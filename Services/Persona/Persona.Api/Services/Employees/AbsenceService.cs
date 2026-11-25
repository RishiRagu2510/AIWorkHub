using System.Text.Json;
using Persona.Contracts;
using Persona.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class AbsenceService(
    PersonaDbContext db,
    ITenantContext actor,
    EmployeeAccess access,
    EmployeeEvents events)
{
    private static readonly string[] Kinds = ["CASUAL", "SICK", "EARNED", "UNPAID", "WFH"];

    private static DateOnly Today =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Kolkata").DateTime);

    private static void RequireYear(int year)
    {
        if (year < 2000 || year > 2200)
            throw new EmployeeProblem(400, "Choose a year between 2000 and 2200.", "ABSENCE_VALIDATION");
    }

    private async Task<PolicyRow> Policy(int year, CancellationToken ct)
    {
        return await db.Database.SqlQuery<PolicyRow>(
                $"SELECT casual_days AS \"CasualDays\",sick_days AS \"SickDays\",earned_days AS \"EarnedDays\",holidays::text AS \"Holidays\",version AS \"Version\" FROM employee_absence_policy WHERE tenant_id={actor.TenantId} AND year={year}")
            .SingleOrDefaultAsync(ct) ?? new PolicyRow();
    }

    private IQueryable<Row> Rows()
    {
        return db.Database.SqlQuery<Row>(
            $"SELECT r.id AS \"Id\",r.employee_id AS \"EmployeeId\",e.employee_code AS \"EmployeeCode\",(e.first_name || ' ' || e.last_name) AS \"EmployeeName\",r.kind AS \"Kind\",r.start_date AS \"StartDate\",r.end_date AS \"EndDate\",r.days AS \"Days\",r.reason AS \"Reason\",r.status AS \"Status\",r.decision_note AS \"DecisionNote\",r.decided_by AS \"DecidedBy\",r.created_at AS \"CreatedAt\",r.version AS \"Version\" FROM employee_absence_requests r JOIN employees e ON e.id=r.employee_id AND e.tenant_id=r.tenant_id WHERE r.tenant_id={actor.TenantId}");
    }

    private static AbsenceRequestDto ToDto(Row r)
    {
        return new AbsenceRequestDto(r.Id, r.EmployeeId, r.EmployeeCode, r.EmployeeName, r.Kind, r.StartDate, r.EndDate,
            r.Days, r.Reason,
            r.Status, r.DecisionNote, r.DecidedBy, r.CreatedAt, r.Version);
    }

    private async Task<AbsenceRequestDto> Request(long id, CancellationToken ct)
    {
        return ToDto(await Rows().SingleAsync(r => r.Id == id, ct));
    }

    public static int WorkingDays(DateOnly start, DateOnly end, ISet<DateOnly> holidays)
    {
        var days = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !holidays.Contains(date))
                days++;
        return days;
    }

    private static int? Allowance(PolicyRow p, string kind)
    {
        return kind switch { "CASUAL" => p.CasualDays, "SICK" => p.SickDays, "EARNED" => p.EarnedDays, _ => null };
    }


    public async Task<AbsencePolicyDto> GetPolicy(int year, CancellationToken ct)
    {
        RequireYear(year);
        var p = await Policy(year, ct);
        return new AbsencePolicyDto(year, p.CasualDays, p.SickDays, p.EarnedDays,
            JsonSerializer.Deserialize<DateOnly[]>(p.Holidays)!, p.Version, p.Version > 0);
    }

    public async Task<AbsencePolicyDto> SavePolicy(int year, AbsencePolicyInput input, CancellationToken ct)
    {
        RequireYear(year);
        if (input.Holidays is null || input.Holidays.Length > 366 || input.Holidays.Any(d => d.Year != year))
            throw new EmployeeProblem(400, "Holiday dates must belong to the selected year.", "ABSENCE_VALIDATION");
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            var current = await Policy(year, t);
            if (current.Version != input.Version)
                throw new EmployeeProblem(409, "The leave policy changed. Refresh before saving.", "ABSENCE_CONFLICT");

            if (await Rows().AnyAsync(r => r.StartDate.Year == year, t))
                throw new EmployeeProblem(409,
                    "This year's policy is locked because requests already exist. Configure the next year separately.",
                    "ABSENCE_CONFLICT");
            var holidays = JsonSerializer.Serialize(input.Holidays.Distinct().Order());
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO employee_absence_policy(tenant_id,year,casual_days,sick_days,earned_days,holidays) VALUES({actor.TenantId},{year},{input.CasualDays},{input.SickDays},{input.EarnedDays},{holidays}::jsonb) ON CONFLICT(tenant_id,year) DO UPDATE SET casual_days=EXCLUDED.casual_days,sick_days=EXCLUDED.sick_days,earned_days=EXCLUDED.earned_days,holidays=EXCLUDED.holidays,version=employee_absence_policy.version+1",
                t);
        }, ct);
        return await GetPolicy(year, ct);
    }


    public async Task<AbsenceListDto> List(int year, bool team, bool all, int page, CancellationToken ct)
    {
        if (year == 0) year = Today.Year;
        RequireYear(year);
        page = Math.Clamp(page, 1, 100000);
        var me = await access.Query.SingleOrDefaultAsync(e => e.UserId == actor.UserId, ct);
        var reports = me != null
            ? access.Query.Where(e => e.ManagerEmployeeId == me.Id && e.Id != me.Id).Select(e => e.Id)
            : Enumerable.Empty<long>().AsQueryable();
        var q = Rows().Where(r => r.StartDate.Year == year);
        if (all && access.Can("EMPLOYEE_VIEW"))
        {
        }
        else if (team)
        {
            q = me != null ? q.Where(r => reports.Contains(r.EmployeeId)) : q.Where(r => false);
        }
        else
        {
            q = me != null ? q.Where(r => r.EmployeeId == me.Id) : q.Where(r => false);
        }

        var total = await q.CountAsync(ct);
        var pageSize = all ? 500 : 25;
        var items = await q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).ToListAsync(ct);
        var p = await Policy(year, ct);
        var mine = me != null
            ? await Rows().Where(r =>
                    r.EmployeeId == me.Id && r.StartDate.Year == year &&
                    (r.Status == "PENDING" || r.Status == "APPROVED"))
                .ToListAsync(ct)
            : new List<Row>();
        var balances = Kinds.Where(k => Allowance(p, k).HasValue).Select(k => new AbsenceBalanceDto(k,
            Allowance(p, k)!.Value,
            mine.Where(r => r.Kind == k && r.Status == "APPROVED").Sum(r => r.Days),
            mine.Where(r => r.Kind == k && r.Status == "PENDING").Sum(r => r.Days),
            Allowance(p, k)!.Value - mine.Where(r => r.Kind == k).Sum(r => r.Days))).ToList();
        var manager = me != null
            ? await access.Query
                .Where(e => e.Id == me.ManagerEmployeeId && e.Id != me.Id && e.EmploymentStatus == "ACTIVE")
                .Select(e => new AbsenceManagerDto(e.Id, e.FirstName + " " + e.LastName)).SingleOrDefaultAsync(ct)
            : null;
        return new AbsenceListDto(items.Select(ToDto).ToList(), total, page, pageSize,
            (int)Math.Ceiling((double)total / pageSize), balances, manager, me != null && await reports.AnyAsync(ct),
            p.Version > 0);
    }

    public async Task<AbsenceRequestDto> Create(AbsenceInput input, CancellationToken ct)
    {
        if (!Kinds.Contains(input.Kind) || string.IsNullOrWhiteSpace(input.Reason))
            throw new EmployeeProblem(400, "Choose a request type and enter a reason.", "ABSENCE_VALIDATION");
        RequireYear(input.StartDate.Year);
        if (input.EndDate < input.StartDate || input.EndDate.Year != input.StartDate.Year)
            throw new EmployeeProblem(400,
                "Use a valid date range within one calendar year. Split requests across years.", "ABSENCE_VALIDATION");
        long id = 0;
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            var me = await access.Me(t);
            if (me.EmploymentStatus != "ACTIVE" || input.StartDate < me.JoiningDate || input.EndDate > me.EndDate)
                throw new EmployeeProblem(400, "Request dates must fall within your active employment.",
                    "ABSENCE_VALIDATION");
            if (!await access.Query.AnyAsync(
                    e => e.Id == me.ManagerEmployeeId && e.Id != me.Id && e.EmploymentStatus == "ACTIVE", t))
                throw new EmployeeProblem(400,
                    "Ask an administrator to assign an active manager before requesting leave or WFH.",
                    "ABSENCE_VALIDATION");
            var p = await Policy(input.StartDate.Year, t);
            if (p.Version == 0)
                throw new EmployeeProblem(400, "An administrator must configure the leave policy for this year first.",
                    "ABSENCE_VALIDATION");
            var days = WorkingDays(input.StartDate, input.EndDate,
                JsonSerializer.Deserialize<HashSet<DateOnly>>(p.Holidays)!);
            if (days == 0)
                throw new EmployeeProblem(400, "The range must include at least one working day.",
                    "ABSENCE_VALIDATION");
            var active = Rows().Where(r => r.EmployeeId == me.Id && (r.Status == "PENDING" || r.Status == "APPROVED"));
            if (await active.AnyAsync(r => r.StartDate <= input.EndDate && r.EndDate >= input.StartDate, t))
                throw new EmployeeProblem(400, "These dates overlap an existing leave or WFH request.",
                    "ABSENCE_VALIDATION");
            var used = await active.Where(r => r.Kind == input.Kind && r.StartDate.Year == input.StartDate.Year)
                .SumAsync(r => (int?)r.Days, t) ?? 0;
            if (Allowance(p, input.Kind) is int limit && used + days > limit)
                throw new EmployeeProblem(400, "Insufficient leave balance, including pending requests.",
                    "ABSENCE_VALIDATION");
            id = await db.Database
                .SqlQuery<long>(
                    $"INSERT INTO employee_absence_requests(tenant_id,employee_id,kind,start_date,end_date,days,reason) VALUES({actor.TenantId},{me.Id},{input.Kind},{input.StartDate},{input.EndDate},{days},{input.Reason.Trim()}) RETURNING id AS \"Value\"")
                .AsAsyncEnumerable().SingleAsync(t);
            await events.Write(me, "ABSENCE_REQUESTED", null,
                new { requestId = id, input.Kind, input.StartDate, input.EndDate, days }, t);
        }, ct);
        return await Request(id, ct);
    }


    public async Task<AbsenceRequestDto> Decide(long id, AbsenceDecision input, bool isHr, CancellationToken ct)
    {
        if (input.Action is not "APPROVE" and not "REJECT" and not "CANCEL")
            throw new EmployeeProblem(400, "Invalid decision.", "ABSENCE_VALIDATION");
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            var row = await Rows().SingleOrDefaultAsync(r => r.Id == id, t) ??
                      throw new EmployeeProblem(404, "Request not found.");
            var employee = await access.Get(row.EmployeeId, t);
            if (input.Action == "CANCEL")
            {
                if (employee.UserId != actor.UserId)
                    throw new EmployeeProblem(403, "Only the requester can cancel this request.");
                if (row.Status != "PENDING")
                    throw new EmployeeProblem(409, "Only pending requests can be cancelled.", "ABSENCE_CONFLICT");
            }
            else
            {
                var isManager = await access.Query.AnyAsync(
                    e => e.Id == employee.ManagerEmployeeId && e.UserId == actor.UserId &&
                         e.EmploymentStatus == "ACTIVE", t);
                if (employee.UserId == actor.UserId || !(isManager || isHr))
                    throw new EmployeeProblem(403,
                        "Only the employee's current manager or HR can approve or reject this request.");
                if (row.Status != "PENDING")
                    throw new EmployeeProblem(409, "This request has already been processed.", "ABSENCE_CONFLICT");
                if (input.Action == "REJECT" && string.IsNullOrWhiteSpace(input.Note))
                    throw new EmployeeProblem(400, "Enter a reason for rejection.", "ABSENCE_VALIDATION");
                if (input.Action == "APPROVE" && (employee.EmploymentStatus != "ACTIVE" ||
                                                  row.StartDate < employee.JoiningDate ||
                                                  row.EndDate > employee.EndDate))
                    throw new EmployeeProblem(400, "The employee is no longer eligible for these dates.",
                        "ABSENCE_VALIDATION");
            }

            if (input.Version != row.Version)
                throw new EmployeeProblem(409, "This request changed. Refresh and retry.", "ABSENCE_CONFLICT");
            var status = input.Action switch { "APPROVE" => "APPROVED", "REJECT" => "REJECTED", _ => "CANCELLED" };
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employee_absence_requests SET status={status},decision_note={input.Note},decided_by={actor.UserId},updated_at=now(),version=version+1 WHERE tenant_id={actor.TenantId} AND id={id}",
                t);
            await events.Write(employee, "ABSENCE_" + status, new { requestId = id, row.Status },
                new { requestId = id, status }, t);
        }, ct);
        return await Request(id, ct);
    }

    private sealed class PolicyRow
    {
        public int CasualDays { get; set; } = 12;
        public int SickDays { get; set; } = 12;
        public int EarnedDays { get; set; } = 12;
        public string Holidays { get; set; } = "[]";
        public int Version { get; set; }
    }

    private sealed class Row
    {
        public long Id { get; set; }
        public long EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string Kind { get; set; } = "";
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int Days { get; set; }
        public string Reason { get; set; } = "";
        public string Status { get; set; } = "";
        public string? DecisionNote { get; set; }
        public long? DecidedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int Version { get; set; }
    }
}
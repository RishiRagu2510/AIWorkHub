using System.Globalization;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;



public class GetCompanyHolidaysTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "get_company_holidays";
    public override string Description =>
        "The company holiday calendar for a year, with the weekday of each holiday, how far away it is and whether it makes a long weekend (a Monday or Friday). " +
        "Also works out a date range: pass fromDate and toDate to get the working days in it (Monday to Friday minus company holidays), which is how many leave days a request over those dates would use. " +
        "Use it for 'what are the holidays this year', 'when is the next holiday', 'is 25 December a holiday' or 'how many leave days is 22 to 26 December'. " +
        "It does not change anything and does not show the user's leave balance (check_employee_leaves does).";
    public override string Category => "Persona";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["year"] = new { type = "INTEGER", description = "The calendar year to list. Defaults to this year." },
            ["fromDate"] = new { type = "STRING", description = "First day of a range to count working days in, YYYY-MM-DD." },
            ["toDate"] = new { type = "STRING", description = "Last day of the range, YYYY-MM-DD. Same as fromDate for one day." }
        }
    };

    private static ToolResult Fail(string message) => new(false, null, message);
    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = TodayLocal;
            var gotFrom = Str(arguments, "fromDate");
            var gotTo = Str(arguments, "toDate");
            var isRange = gotFrom != null || gotTo != null;

            DateOnly from = default, to = default;
            if (gotFrom != null && !TryDate(gotFrom, out from)) return Fail("fromDate must be written as YYYY-MM-DD.");
            if (gotTo != null && !TryDate(gotTo, out to)) return Fail("toDate must be written as YYYY-MM-DD.");
            if (gotTo == null) to = from;
            if (gotFrom == null) from = to;

            int[] years;
            if (isRange)
            {
                if (from > to) return Fail("fromDate is after toDate.");
                if (to.DayNumber - from.DayNumber > 365) return Fail("I can count at most a year at a time.");
                years = Enumerable.Range(from.Year, to.Year - from.Year + 1).ToArray();
            }
            else
            {
                var year = Int(arguments, "year") ?? today.Year;
                if (year is < 2000 or > 2200) return Fail("Choose a year between 2000 and 2200.");
                years = [year];
            }

            var holidays = new SortedSet<DateOnly>();
            var unconfigured = new List<int>();
            var identity = Identity();
            foreach (var year in years)
            {
                using var policy = await TryGetAsync(identity, $"{IdentityRoutes.AbsencePolicy}?year={year}", cancellationToken);
                if (policy == null) return Fail("I could not read the holiday calendar right now.");
                if (policy.RootElement.TryGetProperty("configured", out var configured) && configured.ValueKind == JsonValueKind.False) unconfigured.Add(year);
                if (policy.RootElement.TryGetProperty("holidays", out var list) && list.ValueKind == JsonValueKind.Array)
                    foreach (var h in list.EnumerateArray())
                        if (h.ValueKind == JsonValueKind.String && DateOnly.TryParse(h.GetString(), CultureInfo.InvariantCulture, out var date)) holidays.Add(date);
            }

            var gaps = unconfigured.Count == 0 ? null : $"The holiday calendar has not been set up for {string.Join(" or ", unconfigured)}, so no holidays are known for {(unconfigured.Count == 1 ? "that year" : "those years")}.";

            if (isRange)
            {
                var workingDays = 0;
                var weekendDays = 0;
                var inRange = new List<object>();
                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    var weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    if (weekend) weekendDays++;
                    if (holidays.Contains(d)) inRange.Add(new { date = Day(d), weekday = d.DayOfWeek.ToString(), fallsOnWeekend = weekend });
                    else if (!weekend) workingDays++;
                }

                return new ToolResult(true, JsonSerializer.Serialize(new
                {
                    range = new { from = Day(from), to = Day(to), calendarDays = to.DayNumber - from.DayNumber + 1, workingDays, weekendDays, holidays = inRange },
                    leaveDaysUsed = workingDays,
                    note = from.Year != to.Year
                        ? "A leave request must fall within one calendar year, so a range across New Year has to be requested as two separate requests."
                        : gaps
                }));
            }

            var year0 = years[0];
            var rows = holidays.Where(h => h.Year == year0).Select(h =>
            {
                var weekend = h.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                var away = h.DayNumber - today.DayNumber;
                return new
                {
                    date = Day(h),
                    weekday = h.DayOfWeek.ToString(),
                    daysAway = away,
                    isPast = away < 0,
                    fallsOnWeekend = weekend,
                    makesLongWeekend = h.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Friday
                };
            }).ToList();

            var next = rows.FirstOrDefault(r => !r.isPast);
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                year = year0,
                today = Day(today),
                count = rows.Count,
                upcomingCount = rows.Count(r => !r.isPast),
                next,
                holidays = rows,
                note = gaps ?? (rows.Count == 0 ? "No holidays are listed for that year." : null)
            }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}


public class FindPeopleTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    private const int PageSize = 100;
    private const int MaxPages = 5;
    private const int DefaultShown = 25;
    private const int MostShown = 50;

    public override string Name => "find_people";
    public override string Description =>
        "Search the company directory. Filter by name, employee code or email (query), department, designation (the role, such as 'Engineer'), jobTitle, location, or manager (to list a person's direct reports). Filters combine. " +
        "Returns only work details: name, employee code, work email, designation, department, job title, location, manager and status. By default only active employees. " +
        "Use it for 'who is in Engineering', 'find Ravi', 'who are the designers', 'who is based in Chennai', 'who reports to Priya'. " +
        "For one person's full profile use get_employee_profile. It cannot search skills, pay or personal details.";
    public override string Category => "Persona";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "Part of a name, an employee code or an email." },
            ["department"] = new { type = "STRING", description = "Department name, e.g. 'Engineering'." },
            ["designation"] = new { type = "STRING", description = "Designation (role) name, e.g. 'Software Engineer'." },
            ["jobTitle"] = new { type = "STRING", description = "Job title, e.g. 'UI Designer'." },
            ["location"] = new { type = "STRING", description = "Work location, e.g. 'Chennai'." },
            ["manager"] = new { type = "STRING", description = "A manager's name: lists the people who report to them." },
            ["status"] = new { type = "STRING", description = "ACTIVE (default), or ALL to include people who have left or are on leave." },
            ["limit"] = new { type = "INTEGER", description = $"How many people to show, up to {MostShown}. Defaults to {DefaultShown}." }
        }
    };

    private static ToolResult Fail(string message) => new(false, null, message);

    private sealed record Person(long Id, string Name, string Code, string? Email, long? DepartmentId, long? DesignationId, long? JobTitleId, long? LocationId, long? ManagerId, string? Status);

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var identity = Identity();
            var status = (Str(arguments, "status") ?? "ACTIVE").Trim().ToUpperInvariant();
            var limit = Math.Clamp(Int(arguments, "limit") ?? DefaultShown, 1, MostShown);

            var refs = await EmployeeDirectory.ReferencesAsync(identity, cancellationToken);

            
            var filters = new List<(string Kind, HashSet<long> Ids)>();
            foreach (var (argument, kind, label) in new[] { ("department", "DEPARTMENT", "department"), ("designation", "DESIGNATION", "designation"), ("jobTitle", "JOB_TITLE", "job title"), ("location", "LOCATION", "location") })
            {
                var said = Str(arguments, argument);
                if (said == null) continue;
                var ofKind = refs.Where(r => r.Value.Kind == kind).ToList();
                var exact = ofKind.Where(r => string.Equals(r.Value.Name, said.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                var hits = exact.Count > 0 ? exact : ofKind.Where(r => r.Value.Name.Contains(said.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (hits.Count == 0)
                    return Fail(ofKind.Count == 0
                        ? $"I could not look up {label} names right now."
                        : $"No {label} is called '{said}'. Options: {string.Join(", ", ofKind.Select(r => r.Value.Name).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Take(20))}.");
                filters.Add((kind, hits.Select(h => h.Key).ToHashSet()));
            }

            
            
            var queryWords = (Str(arguments, "query") ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var url = $"&PageSize={PageSize}" + (queryWords.Length > 0 ? $"&Search={Uri.EscapeDataString(queryWords[0])}" : "") + (status == "ALL" ? "" : $"&EmploymentStatus={Uri.EscapeDataString(status)}");
            var everyone = new List<Person>();
            long reported = 0;
            for (var page = 1; page <= MaxPages; page++)
            {
                using var response = await identity.GetAsync($"{IdentityRoutes.Employees}?Page={page}{url}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return page == 1 ? Fail("I could not search the directory right now.") : Fail("The directory stopped answering part way through, so I did not show a partial list.");
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = doc.RootElement;
                if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) break;
                foreach (var e in items.EnumerateArray())
                {
                    if (LongProp(e, "id") is not { } id) continue;
                    everyone.Add(new Person(id, $"{Text(e, "firstName")} {Text(e, "lastName")}".Trim(), Text(e, "employeeCode") ?? "", Text(e, "workEmail"),
                        LongProp(e, "departmentId"), LongProp(e, "designationId"), LongProp(e, "jobTitleId"), LongProp(e, "locationId"), LongProp(e, "managerEmployeeId"), Text(e, "employmentStatus")));
                }
                reported = LongProp(root, "totalCount") ?? everyone.Count;
                var totalPages = (int)(LongProp(root, "totalPages") ?? 1);
                if (page >= totalPages) break;
            }

            
            long? managerId = null;
            if (Str(arguments, "manager") is { } managerSaid)
            {
                var managerWords = managerSaid.Split([' ', '.', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var pool = await EmployeeDirectory.LoadAsync(identity, cancellationToken, managerWords.FirstOrDefault());
                var found = PersonMatch.Find(managerSaid.Trim(), pool.Select(p => new AssigneeCandidate(p.EmployeeId, p.Name, p.Email)).ToList());
                if (found.Count == 0) return Fail($"I could not find anyone called '{managerSaid}'.");
                if (found.Count > 1) return Fail($"'{managerSaid}' fits more than one person: {string.Join(", ", found.Take(8).Select(m => $"{m.Name} ({pool.First(p => p.EmployeeId == m.UserId).Code})"))}. Ask which one.");
                managerId = found[0].UserId;
            }

            var matches = everyone
                .Where(p => filters.All(f => f.Kind switch
                {
                    "DEPARTMENT" => p.DepartmentId is { } d && f.Ids.Contains(d),
                    "DESIGNATION" => p.DesignationId is { } g && f.Ids.Contains(g),
                    "JOB_TITLE" => p.JobTitleId is { } j && f.Ids.Contains(j),
                    _ => p.LocationId is { } l && f.Ids.Contains(l)
                }))
                .Where(p => managerId is not { } m || p.ManagerId == m)
                .Where(p => queryWords.All(w => $"{p.Name} {p.Code} {p.Email}".Contains(w, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            
            var managerNames = (await EmployeeDirectory.LoadAsync(identity, cancellationToken)).ToDictionary(p => p.EmployeeId, p => p.Name);

            string? NameOfRef(long? id) => id is { } i && refs.TryGetValue(i, out var r) ? r.Name : null;
            var shown = matches.Take(limit).Select(p => new
            {
                name = p.Name,
                employeeCode = p.Code,
                workEmail = p.Email,
                designation = NameOfRef(p.DesignationId),
                department = NameOfRef(p.DepartmentId),
                jobTitle = NameOfRef(p.JobTitleId),
                location = NameOfRef(p.LocationId),
                manager = p.ManagerId is { } mid ? managerNames.GetValueOrDefault(mid) : null,
                status = p.Status
            }).ToList();

            string? note = null;
            if (matches.Count == 0) note = "Nobody matches.";
            else if (matches.Count > shown.Count) note = $"Showing {shown.Count} of {matches.Count} people, by name. Ask for a narrower search to see the rest.";
            if (reported > everyone.Count) note = (note == null ? "" : note + " ") + $"Only the first {everyone.Count} of {reported} people in the directory were searched, so there may be more matches.";

            return new ToolResult(true, JsonSerializer.Serialize(new { count = matches.Count, people = shown, note }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}

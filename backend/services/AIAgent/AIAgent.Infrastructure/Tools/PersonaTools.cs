using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;


public abstract class ServiceToolBase : BaseAgentTool
{
    private readonly IHttpClientFactory _factory;
    private readonly IHttpContextAccessor _accessor;

    protected ServiceToolBase(IHttpClientFactory factory, IHttpContextAccessor accessor)
    {
        _factory = factory;
        _accessor = accessor;
    }

    protected HttpClient Identity() => Create("IdentityTenantService");
    protected HttpClient Jira() => Create("JiraService");

    private HttpClient Create(string name)
    {
        var client = _factory.CreateClient(name);
        AttachBearerToken(client, _accessor);
        return client;
    }

    
    protected static async Task<JsonDocument?> TryGetAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    protected static string? StrOf(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.ToString(), _ => null }
            : null;

    protected static string Plural(long n, string word) => n == 1 ? $"{n} {word}" : $"{n} {word}s";
}


internal static class LeaveRules
{
    public static string? Kind(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var key = input.Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');
        return key switch
        {
            "SICK" or "SICK_LEAVE" or "SL" => "SICK",
            "CASUAL" or "CASUAL_LEAVE" or "CL" or "FLOATING" or "FLOATING_HOLIDAY" or "FLOATING_HOLIDAYS" => "CASUAL",
            "EARNED" or "EARNED_LEAVE" or "ANNUAL" or "ANNUAL_LEAVE" or "ANNUAL_PTO" or "PTO" or "PRIVILEGE" or "PRIVILEGE_LEAVE" or "PL" or "VACATION" => "EARNED",
            "UNPAID" or "UNPAID_LEAVE" or "LOP" or "LEAVE_WITHOUT_PAY" => "UNPAID",
            "WFH" or "WORK_FROM_HOME" or "REMOTE" => "WFH",
            _ => null
        };
    }

    public static string Label(string? kind) => kind switch
    {
        
        "SICK" => "Sick leave",
        "CASUAL" => "Floating holidays",
        "EARNED" => "Earned leave",
        "UNPAID" => "Unpaid leave",
        "WFH" => "Work from home",
        _ => kind ?? "Leave"
    };

    public static int WorkingDays(DateOnly start, DateOnly end, ISet<DateOnly> holidays)
    {
        var days = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !holidays.Contains(d)) days++;
        return days;
    }

    
    public static List<object> Balances(JsonElement balances)
    {
        var list = new List<object>();
        if (balances.ValueKind != JsonValueKind.Array) return list;
        foreach (var b in balances.EnumerateArray())
        {
            static long N(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? (long)v.GetDouble() : 0;
            var kind = b.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            list.Add(new { kind, label = Label(kind), allocated = N(b, "allowance"), used = N(b, "used"), pending = N(b, "pending"), available = N(b, "available") });
        }

        return list;
    }
}




public class ApplyLeaveTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "apply_leave";
    public override string Description => "Submit a leave or work-from-home request for the signed-in user. Needs the type (CASUAL for Floating holidays, SICK, EARNED, UNPAID or WFH) and a start and end date. If the user has not given both the type and the dates, call prepare_leave instead of asking. The balance is checked first, and a request over the remaining quota is refused with a message to show the user as written.";
    public override string Category => "Persona";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["leaveType"] = new { type = "STRING", description = "CASUAL (Floating holidays), SICK, EARNED, UNPAID or WFH." },
            ["startDate"] = new { type = "STRING", description = "First day, YYYY-MM-DD." },
            ["endDate"] = new { type = "STRING", description = "Last day, YYYY-MM-DD. Same as startDate for a single day." },
            ["reason"] = new { type = "STRING", description = "Short reason, as the user gave it. Do not invent one." }
        },
        required = new[] { "leaveType", "startDate", "endDate" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var kind = LeaveRules.Kind(Str(arguments, "leaveType"));
            if (kind == null)
                return new ToolResult(false, null, "Which type is it: Floating holidays, Sick leave, Earned leave, Unpaid leave or Work from home?");
            if (!TryDate(Str(arguments, "startDate"), out var start))
                return new ToolResult(false, null, "I need the first day of leave as YYYY-MM-DD.");
            var end = start;
            if (Str(arguments, "endDate") is { } endText && !TryDate(endText, out end))
                return new ToolResult(false, null, "endDate must be written as YYYY-MM-DD.");
            if (end < start) return new ToolResult(false, null, "The end date is before the start date.");
            if (end.Year != start.Year)
                return new ToolResult(false, null, "A request cannot span two calendar years. Please split it at 31 December.");

            var client = Identity();

            
            using var policyDoc = await TryGetAsync(client, $"{IdentityRoutes.AbsencePolicy}?year={start.Year}", cancellationToken);
            if (policyDoc == null)
                return new ToolResult(false, null, "I could not read the leave policy, so I did not submit anything. Please try again in a moment.");
            var policy = policyDoc.RootElement;
            if (policy.TryGetProperty("configured", out var configured) && configured.ValueKind == JsonValueKind.False)
                return new ToolResult(false, null, $"The leave policy for {start.Year} has not been configured yet. Please ask an administrator to set it up.");

            var holidays = new HashSet<DateOnly>();
            if (policy.TryGetProperty("holidays", out var hs) && hs.ValueKind == JsonValueKind.Array)
                foreach (var h in hs.EnumerateArray())
                    if (h.ValueKind == JsonValueKind.String && DateOnly.TryParse(h.GetString(), CultureInfo.InvariantCulture, out var hd)) holidays.Add(hd);

            var days = LeaveRules.WorkingDays(start, end, holidays);
            if (days == 0)
                return new ToolResult(false, null, "That range has no working days (weekends and company holidays are not counted). Choose other dates.");

            using var listDoc = await TryGetAsync(client, $"{IdentityRoutes.Absence}?year={start.Year}&page=1", cancellationToken);
            if (listDoc == null)
                return new ToolResult(false, null, "I could not read your leave balance, so I did not submit anything. Please try again in a moment.");

            long? available = null;
            string? managerName = null;
            if (listDoc.RootElement.TryGetProperty("balances", out var balances) && balances.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in balances.EnumerateArray())
                    if (StrOf(b, "kind") == kind) available = LongProp(b, "available");
            }
            if (listDoc.RootElement.TryGetProperty("manager", out var mgr)) managerName = StrOf(mgr, "name");

            if (available.HasValue && days > available.Value)
            {
                var message = available.Value > 0
                    ? $"Request exceeds remaining quota: {Plural(days, "day")} requested, but only {Plural(available.Value, "day")} available. Would you like to apply for {Plural(available.Value, "day")} instead or use unpaid leave?"
                    : $"Request exceeds remaining quota: {Plural(days, "day")} requested, but you have no {LeaveRules.Label(kind)} days left. Would you like to use unpaid leave instead?";
                return new ToolResult(false, null, message);
            }

            var reason = Str(arguments, "reason") ?? "Requested through Athenna";
            var response = await client.PostAsJsonAsync(IdentityRoutes.Absence, new
            {
                kind,
                startDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDate = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                reason
            }, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"The leave request was not submitted: {ApiMessage(body, (int)response.StatusCode)}");

            using var created = JsonDocument.Parse(body);
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                applied = true,
                requestId = LongProp(created.RootElement, "id"),
                leaveType = kind,
                label = LeaveRules.Label(kind),
                startDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDate = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                workingDays = LongProp(created.RootElement, "days") ?? days,
                status = StrOf(created.RootElement, "status") ?? "PENDING",
                reason,
                remainingAfter = available.HasValue ? available.Value - days : (long?)null,
                awaitingApprovalFrom = managerName
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class PrepareLeaveTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    private static readonly string[] Kinds = ["CASUAL", "SICK", "EARNED", "UNPAID", "WFH"];

    public override string Name => "prepare_leave";
    public override string Description => "Show the user an Apply for leave form in the chat: the five types (Floating holidays, Sick leave, Earned leave, Unpaid leave, Work from home) as buttons with the days left, start and end dates and a reason. Call this whenever the user wants to apply for leave or work from home and has not given BOTH the type and the dates. Pass leaveType only if they named the type. The form is the answer: reply with one short sentence and never ask them to type the type or the dates.";
    public override string Category => "Persona";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["leaveType"] = new { type = "STRING", description = "CASUAL (Floating holidays), SICK, EARNED, UNPAID or WFH, only if the user named it." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var year = DateTime.UtcNow.Year;
            var client = Identity();

            using var listDoc = await TryGetAsync(client, $"{IdentityRoutes.Absence}?year={year}&page=1", cancellationToken);
            if (listDoc == null)
                return new ToolResult(false, null, "I could not load your leave details just now, so I could not open the form. Please try again in a moment.");

            var balance = new Dictionary<string, (long Allowance, long Used, long Pending, long Left)>();
            var root = listDoc.RootElement;
            if (root.TryGetProperty("balances", out var balances) && balances.ValueKind == JsonValueKind.Array)
                foreach (var b in balances.EnumerateArray())
                    if (StrOf(b, "kind") is { } kind && LongProp(b, "available") is { } left)
                        balance[kind] = (LongProp(b, "allowance") ?? left, LongProp(b, "used") ?? 0, LongProp(b, "pending") ?? 0, left);

            string? manager = root.TryGetProperty("manager", out var mgr) ? StrOf(mgr, "name") : null;
            var configured = !(root.TryGetProperty("policyConfigured", out var pc) && pc.ValueKind == JsonValueKind.False);

            var holidays = new List<string>();
            using var policyDoc = await TryGetAsync(client, $"{IdentityRoutes.AbsencePolicy}?year={year}", cancellationToken);
            if (policyDoc != null && policyDoc.RootElement.TryGetProperty("holidays", out var hs) && hs.ValueKind == JsonValueKind.Array)
                foreach (var h in hs.EnumerateArray())
                    if (h.ValueKind == JsonValueKind.String && h.GetString() is { } day) holidays.Add(day);

            var selected = LeaveRules.Kind(Str(arguments, "leaveType"));
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                form = "leave",
                year,
                policyConfigured = configured,
                manager,
                selectedKind = selected,
                holidays,
                
                types = Kinds.Select(k => new
                {
                    kind = k,
                    label = LeaveRules.Label(k),
                    allowance = balance.TryGetValue(k, out var x) ? x.Allowance : (long?)null,
                    used = balance.TryGetValue(k, out var u) ? u.Used : (long?)null,
                    pending = balance.TryGetValue(k, out var pn) ? pn.Pending : (long?)null,
                    available = balance.TryGetValue(k, out var l) ? l.Left : (long?)null
                })
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class PrepareSkillsTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "prepare_skills";
    public override string Description => "Show the user an Update my skills form in the chat: the skills already on their profile (each can be picked to change its level and years, or removed) and the skills they can add from the company skill list, with level buttons. Call this whenever the user wants to update, edit, change, manage or add skills and has not given the skill name, level and years. The form is the answer: reply with one short sentence and never ask them to type the skill name.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_UPDATE_SELF";

    public override object ParameterSchema => new { type = "OBJECT", properties = new Dictionary<string, object>() };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = Identity();
            using var me = await TryGetAsync(client, IdentityRoutes.Me, cancellationToken);
            if (me == null) return new ToolResult(false, null, "I could not load your profile just now, so I could not open the skills form. Please try again in a moment.");
            using var mine = await TryGetAsync(client, IdentityRoutes.MySkills, cancellationToken);
            using var catalog = await TryGetAsync(client, IdentityRoutes.CareerCatalog, cancellationToken);
            if (mine == null || mine.RootElement.ValueKind != JsonValueKind.Array)
                return new ToolResult(false, null, "I could not load your skills just now, so I could not open the skills form. Please try again in a moment.");

            var version = LongProp(me.RootElement, "version");
            if (version == null) return new ToolResult(false, null, "Your profile did not report a version, so I could not open the skills form.");

            static decimal Years(JsonElement e) => e.TryGetProperty("yearsOfExperience", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetDecimal() : 0m;
            var skills = mine.RootElement.EnumerateArray().Select(e => new
            {
                id = LongProp(e, "id"),
                skillId = LongProp(e, "skillId"),
                skill = StrOf(e, "skillName"),
                category = StrOf(e, "category"),
                proficiency = LongProp(e, "proficiency"),
                yearsOfExperience = Years(e),
                lastUsed = StrOf(e, "lastUsedDate")
            }).ToList();

            var available = new List<object>();
            if (catalog != null && catalog.RootElement.TryGetProperty("skills", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                var held = skills.Where(x => x.skillId.HasValue).Select(x => x.skillId!.Value).ToHashSet();
                foreach (var c in list.EnumerateArray())
                    if (LongProp(c, "id") is { } id && !held.Contains(id) && StrOf(c, "name") is { } name)
                        available.Add(new { id, name, category = StrOf(c, "category") });
            }

            return new ToolResult(true, JsonSerializer.Serialize(new { form = "skills", version, skills, available }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class ApproveLeaveRequestTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "approve_leave_request";
    public override string Description => "For managers and HR: list the pending leave and WFH requests of the user's direct reports, or of everyone for HR (action 'list'), or approve or reject one (action 'approve' or 'reject' with its requestId). Only call approve or reject after the user has explicitly said which request and which decision. Rejecting needs a short reason.";
    public override string Category => "Persona";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["action"] = new { type = "STRING", description = "'list' (default), 'approve' or 'reject'." },
            ["requestId"] = new { type = "INTEGER", description = "The request to decide, from the list. Required for approve and reject." },
            ["note"] = new { type = "STRING", description = "Reason for rejecting (required) or an optional comment when approving." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var action = (Str(arguments, "action") ?? "list").ToLowerInvariant();
            if (action is "decline") action = "reject";
            if (action is not ("list" or "approve" or "reject"))
                return new ToolResult(false, null, "action must be 'list', 'approve' or 'reject'.");

            var client = Identity();
            var pending = new List<(JsonElement Row, int Year)>();
            var docs = new List<JsonDocument>();
            var hasReports = false;
            var callerIsHr = InAnyRole(arguments, "HR");
            
            var scopeFlag = callerIsHr ? "all=true" : "team=true";
            try
            {
                var ownPending = new HashSet<long>();
                if (callerIsHr)
                {
                    for (var page = 1; page <= 5; page++)
                    {
                        var mineDoc = await TryGetAsync(client, $"{IdentityRoutes.Absence}?year={TodayLocal.Year}&page={page}", cancellationToken);
                        if (mineDoc == null) break;
                        using (mineDoc)
                        {
                            if (mineDoc.RootElement.TryGetProperty("items", out var mine) && mine.ValueKind == JsonValueKind.Array)
                                foreach (var m in mine.EnumerateArray()) if (LongProp(m, "id") is { } mid) ownPending.Add(mid);
                            if (page >= (LongProp(mineDoc.RootElement, "totalPages") ?? 1)) break;
                        }
                    }
                    for (var page = 1; page <= 5; page++)
                    {
                        var nextYear = await TryGetAsync(client, $"{IdentityRoutes.Absence}?year={TodayLocal.Year + 1}&page={page}", cancellationToken);
                        if (nextYear == null) break;
                        using (nextYear)
                        {
                            if (nextYear.RootElement.TryGetProperty("items", out var mine) && mine.ValueKind == JsonValueKind.Array)
                                foreach (var m in mine.EnumerateArray()) if (LongProp(m, "id") is { } mid) ownPending.Add(mid);
                            if (page >= (LongProp(nextYear.RootElement, "totalPages") ?? 1)) break;
                        }
                    }
                }

                var thisYear = TodayLocal.Year;
                for (var year = thisYear; year <= thisYear + 1; year++)
                {
                    for (var page = 1; page <= 5; page++)
                    {
                        var doc = await TryGetAsync(client, $"{IdentityRoutes.Absence}?year={year}&{scopeFlag}&page={page}", cancellationToken);
                        if (doc == null) { if (year == thisYear && page == 1) return new ToolResult(false, null, "I could not read the leave requests right now."); break; }
                        docs.Add(doc);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("hasReports", out var hr) && hr.ValueKind == JsonValueKind.True) hasReports = true;
                        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                            foreach (var item in items.EnumerateArray())
                                if (StrOf(item, "status") == "PENDING" && !(LongProp(item, "id") is { } pid && ownPending.Contains(pid)) && !pending.Any(p => LongProp(p.Row, "id") == LongProp(item, "id"))) pending.Add((item, year));
                        var pages = LongProp(root, "totalPages") ?? 1;
                        if (page >= pages) break;
                    }
                }

                if (!hasReports && !callerIsHr)
                    return new ToolResult(false, null, "RBAC: only a manager (for their direct reports) or HR can review leave requests, and you have no direct reports.");

                object Shape(JsonElement r) => new
                {
                    requestId = LongProp(r, "id"),
                    employee = StrOf(r, "employeeName"),
                    employeeCode = StrOf(r, "employeeCode"),
                    leaveType = StrOf(r, "kind"),
                    label = LeaveRules.Label(StrOf(r, "kind")),
                    startDate = StrOf(r, "startDate"),
                    endDate = StrOf(r, "endDate"),
                    days = LongProp(r, "days"),
                    reason = StrOf(r, "reason"),
                    requestedOn = StrOf(r, "createdAt")
                };

                if (action == "list")
                    return new ToolResult(true, JsonSerializer.Serialize(new { pendingCount = pending.Count, requests = pending.Select(p => Shape(p.Row)).ToArray() }));

                var requestId = Long(arguments, "requestId");
                if (requestId == null)
                    return new ToolResult(false, null, "Which request? Please give the request number from the pending list.");
                var note = Str(arguments, "note");
                if (action == "reject" && note == null)
                    return new ToolResult(false, null, "A short reason is needed to reject a request. What should I tell the employee?");

                var match = pending.FirstOrDefault(p => LongProp(p.Row, "id") == requestId);
                if (match.Row.ValueKind != JsonValueKind.Object)
                    return new ToolResult(false, null, $"Request {requestId} is not a pending request you can decide (it must belong to one of your direct reports, or you must be HR, and it cannot be your own).");

                var version = LongProp(match.Row, "version") ?? 0;
                var response = await client.PostAsJsonAsync(IdentityRoutes.AbsenceDecision(requestId),
                    new { action = action == "approve" ? "APPROVE" : "REJECT", version, note }, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new ToolResult(false, null, $"The request was not {(action == "approve" ? "approved" : "rejected")}: {ApiMessage(body, (int)response.StatusCode)}");

                var summary = Shape(match.Row);
                return new ToolResult(true, JsonSerializer.Serialize(new
                {
                    decided = true,
                    decision = action == "approve" ? "APPROVED" : "REJECTED",
                    note,
                    request = summary
                }));
            }
            finally
            {
                foreach (var d in docs) d.Dispose();
            }
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




internal static class PersonaProfiles
{
    
    public static async Task<(JsonDocument? Profile, object? Choices, string? Error)> ResolveAsync(
        HttpClient identity, long? employeeId, string? query, CancellationToken ct)
    {
        string url;
        if (employeeId.HasValue) url = IdentityRoutes.Employee(employeeId.Value);
        else if (string.IsNullOrWhiteSpace(query) || query.Trim().ToLowerInvariant() is "me" or "my" or "myself" or "mine") url = IdentityRoutes.Me;
        else
        {
            var people = await EmployeeDirectory.LoadAsync(identity, ct, query);
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (people.Count == 0 && terms.Length > 1)
                people = (await EmployeeDirectory.LoadAsync(identity, ct, terms.OrderByDescending(t => t.Length).First()))
                    .Where(p => terms.All(t => p.Name.Contains(t, StringComparison.OrdinalIgnoreCase) || p.Code.Equals(t, StringComparison.OrdinalIgnoreCase))).ToList();

            var exact = people.Where(p => p.Name.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase) || p.Code.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) people = exact;

            if (people.Count == 0) return (null, null, $"I could not find an employee matching '{query}'.");
            if (people.Count > 1)
                return (null, people.Take(8).Select(p => new { employeeId = p.EmployeeId, name = p.Name, employeeCode = p.Code, email = p.Email }).ToArray(), null);
            url = IdentityRoutes.Employee(people[0].EmployeeId);
        }

        try
        {
            using var response = await identity.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return (null, null, response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "That employee record was not found, or you cannot view it."
                    : response.StatusCode == System.Net.HttpStatusCode.Forbidden
                        ? "RBAC: you do not have permission to view employee records."
                        : $"The employee service answered HTTP {(int)response.StatusCode}.");
            return (JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)), null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return (null, null, "The employee service could not be reached.");
        }
    }

    




    public static bool SeesWholeProfile(JsonElement profile, long? callerUserId, bool callerIsHrOrAdmin) =>
        callerIsHrOrAdmin
        || (callerUserId.HasValue && profile.TryGetProperty("userId", out var owner) && owner.TryGetInt64(out var ownerId) && ownerId == callerUserId.Value);

    public const string WholeProfileOnlyMessage =
        "That part of a profile (Personal, Skills, Experience, Education, Certifications and Languages) is visible only to the person themself and to HR and Admin. Say so plainly; the Overview, Job, Career history, Projects and Organization tabs are open to everyone.";

    public static async Task<Dictionary<string, object?>> BuildAsync(
        HttpClient identity, HttpClient jira, JsonElement profile, long? callerUserId, bool callerIsAdmin, bool wholeProfile, CancellationToken ct)
    {
        static long? L(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var x) ? x : null;
        static string? S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var refs = await EmployeeDirectory.ReferencesAsync(identity, ct);
        var people = await EmployeeDirectory.LoadAsync(identity, ct);
        string? Ref(string name) => L(profile, name) is { } id && refs.TryGetValue(id, out var r) ? r.Name : null;

        var employeeId = L(profile, "id") ?? 0;
        var userId = L(profile, "userId") ?? 0;
        var managerId = L(profile, "managerEmployeeId");

        
        var chain = new List<string>();
        var seen = new HashSet<long> { employeeId };
        var next = managerId;
        while (next.HasValue && seen.Add(next.Value) && chain.Count < 8)
        {
            var boss = people.FirstOrDefault(p => p.EmployeeId == next.Value);
            if (boss == null) break;
            chain.Add(boss.Name);
            next = boss.ManagerEmployeeId;
        }

        var reports = people.Where(p => p.ManagerEmployeeId == employeeId && p.EmployeeId != employeeId)
            .Select(p => new { employeeId = p.EmployeeId, name = p.Name, employeeCode = p.Code, designation = p.DesignationId is { } d && refs.TryGetValue(d, out var r) ? r.Name : null })
            .ToArray();

        var projects = new List<object>();
        using (var byUser = await Task.Run(async () =>
        {
            try
            {
                using var response = await jira.GetAsync(JiraRoutes.ProjectsByUser(userId), ct);
                return response.IsSuccessStatusCode ? JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)) : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException) { return null; }
        }, ct))
        {
            Dictionary<long, long>? allocation = null;
            if (userId == callerUserId || callerIsAdmin)
            {
                try
                {
                    using var response = await jira.GetAsync($"{JiraRoutes.ProjectAllocations}?userId={userId}", ct);
                    if (response.IsSuccessStatusCode)
                    {
                        using var alloc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                        allocation = [];
                        foreach (var row in alloc.RootElement.EnumerateArray())
                            if (L(row, "projectId") is { } pid && L(row, "percentage") is { } pct) allocation[pid] = pct;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException) { }
            }

            if (byUser != null && byUser.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in byUser.RootElement.EnumerateArray())
                {
                    var pid = L(p, "id");
                    projects.Add(new
                    {
                        projectId = pid,
                        name = S(p, "projectName"),
                        key = S(p, "key"),
                        role = S(p, "role"),
                        status = S(p, "status"),
                        allocationPercent = pid.HasValue && allocation != null && allocation.TryGetValue(pid.Value, out var pc) ? pc : (long?)null
                    });
                }
            }
        }

        
        object[]? skills = null;
        string? skillsNote = null;
        if (!wholeProfile) skillsNote = "Skills are visible only to the person themself and to HR and Admin. Say so; do not claim they have none.";
        else try
        {
            using var skillResponse = await identity.GetAsync(IdentityRoutes.EmployeeSkills(employeeId), ct);
            if (skillResponse.IsSuccessStatusCode)
            {
                using var skillDoc = JsonDocument.Parse(await skillResponse.Content.ReadAsStringAsync(ct));
                string[] scale = ["", "Beginner", "Basic", "Intermediate", "Advanced", "Expert"];
                skills = skillDoc.RootElement.ValueKind == JsonValueKind.Array
                    ? skillDoc.RootElement.EnumerateArray().Select(s => (object)new
                    {
                        skill = S(s, "skillName"),
                        category = S(s, "category"),
                        proficiency = L(s, "proficiency"),
                        proficiencyLabel = L(s, "proficiency") is { } level && level >= 1 && level <= 5 ? scale[level] : null,
                        yearsOfExperience = s.TryGetProperty("yearsOfExperience", out var yrs) && yrs.ValueKind == JsonValueKind.Number ? yrs.GetDouble() : (double?)null,
                        lastUsed = S(s, "lastUsedDate")
                    }).ToArray()
                    : [];
            }
            else
            {
                skillsNote = "Skills could not be read for this person (you may not be allowed to view them). Say so; do not claim they have none.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            skillsNote = "The skills service did not answer. Say so; do not claim they have none.";
        }

        var roles = profile.TryGetProperty("roles", out var r2) && r2.ValueKind == JsonValueKind.Array
            ? r2.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToArray()
            : [];

        var emergency = new { name = S(profile, "emergencyContactName"), phone = S(profile, "emergencyContactPhone"), relationship = S(profile, "emergencyContactRelation") };

        
        return new Dictionary<string, object?>
        {
            ["employeeId"] = employeeId,
            ["userId"] = userId,
            ["employeeCode"] = S(profile, "employeeCode"),
            ["name"] = $"{S(profile, "firstName")} {S(profile, "lastName")}".Trim(),
            ["preferredName"] = S(profile, "preferredName"),
            ["workEmail"] = S(profile, "workEmail"),
            ["phone"] = wholeProfile ? S(profile, "phone") : null,
            ["emergencyContact"] = !wholeProfile || (emergency.name == null && emergency.phone == null) ? null : emergency,
            ["employmentType"] = S(profile, "employmentType"),
            ["employmentStatus"] = S(profile, "employmentStatus"),
            ["joiningDate"] = S(profile, "joiningDate"),
            ["department"] = Ref("departmentId"),
            ["discipline"] = Ref("disciplineId"),
            ["jobTitle"] = Ref("jobTitleId"),
            ["designation"] = Ref("designationId"),
            ["grade"] = Ref("gradeId"),
            ["location"] = Ref("locationId"),
            ["manager"] = managerId.HasValue ? new { employeeId = managerId.Value, name = people.FirstOrDefault(p => p.EmployeeId == managerId.Value)?.Name } : null,
            ["reportingChain"] = chain,
            ["directReports"] = reports,
            ["projects"] = projects,
            ["skills"] = skills,
            ["skillsNote"] = skillsNote,
            ["roles"] = roles
        };
    }
}




public class GetEmployeeProfileTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "get_employee_profile";
    public override string Description => "Show an employee's profile: role and designation, department, manager and reporting chain, direct reports, active projects, and technical skills (with proficiency, years and last used). Use it for my skills and the skills of a named person too. It does not hold the career history (use get_career_history) or the Personal, Experience, Education, Certifications and Languages tabs (use get_employee_record). Pass a name or employee code in 'query', an employeeId, or nothing for the signed-in user. When several people match, the result lists them so the user can choose.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "A name or employee code. Omit for the signed-in user." },
            ["employeeId"] = new { type = "INTEGER", description = "Employee record id, when known from an earlier result." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var identity = Identity();
            var (profile, choices, error) = await PersonaProfiles.ResolveAsync(identity, Long(arguments, "employeeId"), Str(arguments, "query"), cancellationToken);
            if (error != null) return new ToolResult(false, null, error);
            if (choices != null)
                return new ToolResult(true, JsonSerializer.Serialize(new { needsChoice = true, message = "More than one person matches. Ask which one is meant.", candidates = choices }));

            using (profile)
            {
                var view = await PersonaProfiles.BuildAsync(identity, Jira(), profile!.RootElement, CallerUserId(arguments), InAnyRole(arguments, "ADMIN"), PersonaProfiles.SeesWholeProfile(profile.RootElement, CallerUserId(arguments), InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN")), cancellationToken);
                return new ToolResult(true, JsonSerializer.Serialize(view));
            }
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class GetCareerHistoryTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "get_career_history";
    public override string Description => "Show an employee's career history: each period with its start and end (or Present), grade and designation, job title, department, how long it lasted and any note, newest first. Use it for 'career history', 'my career', promotions, role or grade changes, and how long someone has been in their current position. Pass a name or employee code in 'query', an employeeId, or nothing for the signed-in user.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "A name or employee code. Omit for the signed-in user." },
            ["employeeId"] = new { type = "INTEGER", description = "Employee record id, when known from an earlier result." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var identity = Identity();
            var (profile, choices, error) = await PersonaProfiles.ResolveAsync(identity, Long(arguments, "employeeId"), Str(arguments, "query"), cancellationToken);
            if (error != null) return new ToolResult(false, null, error);
            if (choices != null)
                return new ToolResult(true, JsonSerializer.Serialize(new { needsChoice = true, message = "More than one person matches. Ask which one is meant.", candidates = choices }));

            using (profile)
            {
                var root = profile!.RootElement;
                var id = root.TryGetProperty("id", out var idValue) && idValue.TryGetInt64(out var parsedId) ? parsedId : 0;
                var name = $"{StrOf(root, "firstName")} {StrOf(root, "lastName")}".Trim();
                var code = StrOf(root, "employeeCode");

                using var history = await TryGetAsync(identity, IdentityRoutes.EmployeeCareerHistory(id), cancellationToken);
                if (history == null || history.RootElement.ValueKind != JsonValueKind.Array)
                    return new ToolResult(false, null, "The career history could not be read. You may not be allowed to view it.");

                var periods = CareerPeriods.Build(history.RootElement, StrOf(root, "endDate"), DateOnly.FromDateTime(DateTime.Now));
                var message = periods.Count == 0 ? "No career changes recorded yet. Earlier promotions have not been imported." : null;
                return new ToolResult(true, JsonSerializer.Serialize(new { employee = name, employeeCode = code, count = periods.Count, message, periods }));
            }
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class GetEmployeeRecordTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    private static readonly string[] Sections = ["personal", "experience", "education", "certifications", "languages"];

    public override string Name => "get_employee_record";
    public override string Description => "Show one tab of an employee's Persona profile: 'personal' (names, work email, phone, location), 'experience' (earlier jobs), 'education', 'certifications' or 'languages'. Pass a name or employee code in 'query', an employeeId, or nothing for the signed-in user. Skills, job, organization and projects come from get_employee_profile; career history from get_career_history; pay from get_employee_compensation.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["section"] = new { type = "STRING", description = "personal, experience, education, certifications or languages.", @enum = Sections },
            ["query"] = new { type = "STRING", description = "A name or employee code. Omit for the signed-in user." },
            ["employeeId"] = new { type = "INTEGER", description = "Employee record id, when known from an earlier result." }
        },
        required = new[] { "section" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var section = (Str(arguments, "section") ?? "").Trim().ToLowerInvariant();
            if (!Sections.Contains(section))
                return new ToolResult(false, null, $"Section must be one of: {string.Join(", ", Sections)}.");

            var identity = Identity();
            var (profile, choices, error) = await PersonaProfiles.ResolveAsync(identity, Long(arguments, "employeeId"), Str(arguments, "query"), cancellationToken);
            if (error != null) return new ToolResult(false, null, error);
            if (choices != null)
                return new ToolResult(true, JsonSerializer.Serialize(new { needsChoice = true, message = "More than one person matches. Ask which one is meant.", candidates = choices }));

            using (profile)
            {
                var root = profile!.RootElement;
                if (!PersonaProfiles.SeesWholeProfile(root, CallerUserId(arguments), InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN")))
                    return new ToolResult(false, null, "RBAC: " + PersonaProfiles.WholeProfileOnlyMessage);

                var id = root.TryGetProperty("id", out var idValue) && idValue.TryGetInt64(out var parsedId) ? parsedId : 0;
                var name = $"{StrOf(root, "firstName")} {StrOf(root, "lastName")}".Trim();
                var code = StrOf(root, "employeeCode");

                if (section == "personal")
                {
                    string? location = null;
                    if (root.TryGetProperty("locationId", out var loc) && loc.TryGetInt64(out var locationId))
                    {
                        var refs = await EmployeeDirectory.ReferencesAsync(identity, cancellationToken);
                        if (refs.TryGetValue(locationId, out var r)) location = r.Name;
                    }

                    static string Or(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v!;
                    var fields = new[]
                    {
                        new { field = "First name", value = Or(StrOf(root, "firstName")) },
                        new { field = "Last name", value = Or(StrOf(root, "lastName")) },
                        new { field = "Preferred name", value = Or(StrOf(root, "preferredName")) },
                        new { field = "Work email", value = Or(StrOf(root, "workEmail")) },
                        new { field = "Phone", value = Or(StrOf(root, "phone")) },
                        new { field = "Location", value = Or(location) }
                    };
                    return new ToolResult(true, JsonSerializer.Serialize(new { employee = name, employeeCode = code, section, count = fields.Length, message = (string?)null, items = fields }));
                }

                using var entries = await TryGetAsync(identity, IdentityRoutes.EmployeeSection(id, section), cancellationToken);
                if (entries == null || entries.RootElement.ValueKind != JsonValueKind.Array)
                    return new ToolResult(false, null, $"The {section} could not be read. You may not be allowed to view it.");

                var items = entries.RootElement.EnumerateArray().Select(e => Row(section, e)).ToList();
                var message = items.Count == 0 ? $"No {section} recorded yet." : null;
                return new ToolResult(true, JsonSerializer.Serialize(new { employee = name, employeeCode = code, section, count = items.Count, message, items }));
            }
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }

    private static object Row(string section, JsonElement e)
    {
        static string Or(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v!;
        string S(string n) => Or(StrOf(e, n));
        string Date(string n) => StrOf(e, n) is { Length: >= 10 } d ? d[..10] : "—";
        string Span() => $"{Date("startDate")} → {(StrOf(e, "endDate") is { Length: >= 10 } end ? end[..10] : "Present")}";

        return section switch
        {
            "experience" => new { company = S("company"), title = S("title"), period = Span(), description = S("description") },
            "education" => new { institution = S("institution"), degree = S("qualification"), fieldOfStudy = S("fieldOfStudy"), period = Span() },
            "certifications" => new { name = S("name"), issuer = S("issuer"), credentialId = S("credentialId"), issued = Date("issuedDate"), expires = Date("expiryDate"), credentialUrl = S("credentialUrl") },
            _ => (object)new { language = S("name"), proficiency = S("proficiency") }
        };
    }
}


internal static class CareerPeriods
{
    private static string? StrOf(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateOnly? Parse(string? value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d) ? d : null;

    public static string Length(DateOnly? start, DateOnly? end)
    {
        if (start is not { } a || end is not { } b || b < a) return "Not recorded";
        var months = (b.Year - a.Year) * 12 + b.Month - a.Month;
        if (a.AddMonths(months) > b) months--;
        var days = b.DayNumber - a.AddMonths(months).DayNumber;
        return $"{months / 12} year(s), {months % 12} month(s), {days} day(s)";
    }

    public static List<object> Build(JsonElement entries, string? employmentEnd, DateOnly today)
    {
        var endDate = Parse(employmentEnd);
        var until = endDate is { } e && e < today ? e : today;
        var ordered = entries.EnumerateArray().OrderByDescending(x => x.TryGetProperty("id", out var i) && i.TryGetInt64(out var n) ? n : 0).ToList();

        var result = new List<object>();
        for (var index = 0; index < ordered.Count; index++)
        {
            var entry = ordered[index];
            var start = Parse(StrOf(entry, "effectiveDate"));
            var next = index > 0 ? Parse(StrOf(ordered[index - 1], "effectiveDate")) : null;
            var end = next is { } n && n < until ? n : until;
            var current = index == 0 && (endDate is null || endDate > today);
            var changeType = StrOf(entry, "changeType");

            var details = entry.TryGetProperty("details", out var d) ? d : default;
            var gradeDesignation = string.Join(" · ", new[] { StrOf(details, "grade"), StrOf(details, "designation") }.Where(x => !string.IsNullOrWhiteSpace(x)));
            static string Or(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v!;
            var recorded = StrOf(entry, "recordedAt");

            result.Add(new
            {
                number = index + 1,
                period = $"{start?.ToString("yyyy-MM-dd") ?? "Date not recorded"} → {(current ? "Present" : end.ToString("yyyy-MM-dd"))}",
                status = current ? "Current position" : changeType == "BASELINE" ? "Previous recorded details" : "Previous period",
                gradeDesignation = Or(gradeDesignation),
                jobTitle = Or(StrOf(details, "jobTitle")),
                department = Or(StrOf(details, "department")),
                periodLength = Length(start, end),
                notes = Or(StrOf(entry, "note")),
                recorded = recorded is { Length: >= 10 } ? recorded[..10] : "—"
            });
        }

        return result;
    }
}




public class ExportEmployeeDossierTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "export_employee_dossier";
    public override string Description => "Export one employee's full dossier (profile, skills, experience, education, certifications, languages and, where permitted, compensation) as a downloadable CSV file. Only HR and Admin may use it. The file is delivered to the user's chat as a download; its contents are not shown to you.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    
    public override string RequiredPermission => "EMPLOYEE_VIEW_SENSITIVE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "A name or employee code." },
            ["employeeId"] = new { type = "INTEGER", description = "Employee record id, when known." },
            ["format"] = new { type = "STRING", description = "'csv' (the only format available)." }
        }
    };

    private static readonly string[] Sections = ["skills", "experience", "education", "certifications", "languages", "compensation"];
    private static readonly HashSet<string> SkippedFields = new(StringComparer.OrdinalIgnoreCase)
    { "tenantId", "employeeId", "version", "skillId", "fileId", "filePath", "storagePath", "createdAt", "updatedAt" };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN"))
                return new ToolResult(false, null, "RBAC: exporting an employee dossier is limited to HR and Admin roles.");

            var format = (Str(arguments, "format") ?? "csv").ToLowerInvariant();
            if (format != "csv")
                return new ToolResult(false, null, "Only CSV export is available right now; PDF is not supported yet. Would you like the dossier as a CSV file?");

            var identity = Identity();
            var (profile, choices, error) = await PersonaProfiles.ResolveAsync(identity, Long(arguments, "employeeId"), Str(arguments, "query"), cancellationToken);
            if (error != null) return new ToolResult(false, null, error);
            if (choices != null)
                return new ToolResult(true, JsonSerializer.Serialize(new { needsChoice = true, message = "More than one person matches. Ask which one is meant.", candidates = choices }));

            using (profile)
            {
                var view = await PersonaProfiles.BuildAsync(identity, Jira(), profile!.RootElement, CallerUserId(arguments), true, true, cancellationToken);
                var employeeId = (long)view["employeeId"]!;
                var rows = new List<(string Section, string Item, string Field, string Value)>();

                void Add(string section, string item, string field, object? value)
                {
                    var text = value switch { null => null, string s => s, _ => JsonSerializer.Serialize(value) };
                    if (!string.IsNullOrWhiteSpace(text)) rows.Add((section, item, field, text));
                }

                foreach (var (key, value) in view)
                {
                    
                    if (key is "skills" or "skillsNote") continue;
                    switch (value)
                    {
                        case null: break;
                        case string or long or int:
                            Add("profile", "", key, value.ToString());
                            break;
                        default:
                            
                            using (var nested = JsonDocument.Parse(JsonSerializer.Serialize(value)))
                            {
                                var index = 0;
                                if (nested.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var element in nested.RootElement.EnumerateArray()) Flatten("profile", $"{key} #{++index}", element, Add);
                                }
                                else Flatten("profile", key, nested.RootElement, Add);
                            }

                            break;
                    }
                }

                var included = new List<string> { "profile" };
                var skipped = new List<string>();
                foreach (var section in Sections)
                {
                    using var doc = await TryGetAsync(identity, IdentityRoutes.EmployeeSection(employeeId, section), cancellationToken);
                    if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) { skipped.Add(section); continue; }
                    included.Add(section);
                    var index = 0;
                    foreach (var element in doc.RootElement.EnumerateArray()) Flatten(section, $"#{++index}", element, Add);
                }

                var csv = new StringBuilder("section,item,field,value\r\n");
                foreach (var row in rows)
                    csv.Append(Cell(row.Section)).Append(',').Append(Cell(row.Item)).Append(',').Append(Cell(row.Field)).Append(',').Append(Cell(row.Value)).Append("\r\n");

                var code = view["employeeCode"]?.ToString() ?? employeeId.ToString();
                var safeCode = new string(code.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
                var fileName = $"dossier-{(safeCode.Length == 0 ? employeeId.ToString() : safeCode)}-{TodayLocal:yyyyMMdd}.csv";

                return new ToolResult(true, JsonSerializer.Serialize(new
                {
                    exported = true,
                    employee = new { name = view["name"], employeeCode = view["employeeCode"] },
                    format = "csv",
                    sections = included,
                    sectionsNotAvailable = skipped,
                    file = new { fileName, mimeType = "text/csv", content = csv.ToString() }
                }));
            }
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }

    private static void Flatten(string section, string item, JsonElement element, Action<string, string, string, object?> add)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (SkippedFields.Contains(property.Name)) continue;
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    Flatten(section, item, property.Value, (s, i, f, v) => add(s, i, $"{property.Name}.{f}", v));
                else add(section, item, property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString());
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray()) Flatten(section, $"{item}.{++index}", child, add);
        }
        else add(section, item, "value", element.ToString());
    }

    
    internal static string Cell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}




public class UpdateMyProfileTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "update_my_profile";
    public override string Description => "Update the signed-in user's own contact details, emergency contact, or add one technical skill. Never changes designation, compensation, manager or base location; if the user asks for those, pass them in the matching 'restricted' field so the refusal is shown. There is no free-text bio field.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_UPDATE_SELF";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["preferredName"] = new { type = "STRING", description = "Name the user likes to be called." },
            ["phone"] = new { type = "STRING", description = "Contact phone number." },
            ["emergencyContactName"] = new { type = "STRING", description = "Emergency contact's name." },
            ["emergencyContactPhone"] = new { type = "STRING", description = "Emergency contact's phone." },
            ["emergencyContactRelation"] = new { type = "STRING", description = "Relationship, for example Spouse." },
            ["skillName"] = new { type = "STRING", description = "A technical skill to add, for example 'Kubernetes'." },
            ["skillProficiency"] = new { type = "INTEGER", description = "1 (beginner) to 5 (expert). Ask the user if not given." },
            ["skillYears"] = new { type = "NUMBER", description = "Years of experience with the skill. Defaults to 0." },
            ["skillCategory"] = new { type = "STRING", description = "Optional category such as 'Cloud' or 'Backend'." },
            ["restrictedDesignation"] = new { type = "STRING", description = "Only if the user asked to change designation or job title. Triggers the refusal." },
            ["restrictedCompensation"] = new { type = "STRING", description = "Only if the user asked to change CTC, salary or bonus. Triggers the refusal." },
            ["restrictedManager"] = new { type = "STRING", description = "Only if the user asked to change their manager. Triggers the refusal." },
            ["restrictedBaseLocation"] = new { type = "STRING", description = "Only if the user asked to change base location. Triggers the refusal." },
            ["bio"] = new { type = "STRING", description = "Only if the user asked to set a bio. Triggers an explanation that no bio field exists." }
        }
    };

    private static readonly Dictionary<string, string> Restricted = new()
    {
        ["restrictedDesignation"] = "Designation",
        ["restrictedCompensation"] = "CTC",
        ["restrictedManager"] = "Manager",
        ["restrictedBaseLocation"] = "Base Location"
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var blocked = Restricted.Where(r => Str(arguments, r.Key) != null).Select(r => r.Value).ToList();
            if (blocked.Count > 0)
                return new ToolResult(false, null, $"RBAC: {string.Join(", ", blocked)} can only be changed by HR or your administrator, not through self-service. I can update your contact details, emergency contact or technical skills instead.");
            if (Str(arguments, "bio") != null)
                return new ToolResult(false, null, "Profiles do not have a bio field yet, so there is nothing to save it to. I can update your phone, emergency contact or technical skills instead.");

            var preferredName = Str(arguments, "preferredName");
            var phone = Str(arguments, "phone");
            var ecName = Str(arguments, "emergencyContactName");
            var ecPhone = Str(arguments, "emergencyContactPhone");
            var ecRelation = Str(arguments, "emergencyContactRelation");
            var skill = Str(arguments, "skillName");
            var profileChange = preferredName != null || phone != null || ecName != null || ecPhone != null || ecRelation != null;
            if (!profileChange && skill == null)
                return new ToolResult(false, null, "What would you like to change: your phone, emergency contact, or a technical skill?");

            int proficiency = 0;
            if (skill != null)
            {
                proficiency = Int(arguments, "skillProficiency") ?? 0;
                if (proficiency is < 1 or > 5)
                    return new ToolResult(false, null, $"How would you rate your {skill} skill from 1 (beginner) to 5 (expert)?");
            }

            var client = Identity();
            using var meResponse = await client.GetAsync(IdentityRoutes.Me, cancellationToken);
            if (!meResponse.IsSuccessStatusCode)
                return new ToolResult(false, null, $"I could not load your profile: {ApiMessage(await meResponse.Content.ReadAsStringAsync(cancellationToken), (int)meResponse.StatusCode)}");
            using var me = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync(cancellationToken));
            var current = me.RootElement;
            var version = LongProp(current, "version");
            if (version == null) return new ToolResult(false, null, "Your profile did not report a version, so nothing was changed.");

            var changed = new List<string>();

            if (profileChange)
            {
                
                var merged = new Dictionary<string, object?>
                {
                    ["preferredName"] = preferredName ?? StrOf(current, "preferredName"),
                    ["phone"] = phone ?? StrOf(current, "phone"),
                    ["locationId"] = LongProp(current, "locationId"),
                    ["profilePhotoUrl"] = StrOf(current, "profilePhotoUrl"),
                    ["aadharNumber"] = StrOf(current, "aadharNumber"),
                    ["permanentAddress"] = StrOf(current, "permanentAddress"),
                    ["emergencyContactName"] = ecName ?? StrOf(current, "emergencyContactName"),
                    ["emergencyContactPhone"] = ecPhone ?? StrOf(current, "emergencyContactPhone"),
                    ["emergencyContactRelation"] = ecRelation ?? StrOf(current, "emergencyContactRelation")
                };
                using var request = new HttpRequestMessage(HttpMethod.Put, IdentityRoutes.MyProfile) { Content = JsonContent.Create(merged) };
                request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
                using var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new ToolResult(false, null, $"Your profile was not updated: {ApiMessage(body, (int)response.StatusCode)}");

                using var updated = JsonDocument.Parse(body);
                version = LongProp(updated.RootElement, "version") ?? version + 1;
                if (preferredName != null) changed.Add("preferred name");
                if (phone != null) changed.Add("phone");
                if (ecName != null || ecPhone != null || ecRelation != null) changed.Add("emergency contact");
            }

            string? addedSkill = null;
            if (skill != null)
            {
                var payload = new
                {
                    skillName = skill,
                    category = Str(arguments, "skillCategory"),
                    proficiency,
                    yearsOfExperience = Math.Clamp(Convert.ToDecimal(Str(arguments, "skillYears") is { } y && decimal.TryParse(y, NumberStyles.Number, CultureInfo.InvariantCulture, out var yrs) ? yrs : 0m), 0m, 80m),
                    lastUsedDate = TodayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                };
                using var request = new HttpRequestMessage(HttpMethod.Post, IdentityRoutes.MySkills) { Content = JsonContent.Create(payload) };
                request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
                using var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new ToolResult(false, null, $"{(changed.Count > 0 ? "Your details were saved, but " : "")}the skill '{skill}' was not added: {ApiMessage(body, (int)response.StatusCode)}");
                addedSkill = skill;
                changed.Add($"skill {skill} ({proficiency}/5)");
            }

            return new ToolResult(true, JsonSerializer.Serialize(new { updated = true, changed, skillAdded = addedSkill }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class GetCompensationMatrixTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    private const int MaxPeople = 200;

    public override string Name => "get_compensation_matrix";
    public override string Description => "Salary bands by level (Junior, Mid, Senior, Lead, Staff and so on): headcount, CTC minimum, average and maximum, currency and bonus, built from current compensation records. Restricted to HR, Finance and Admin. Never lists individuals.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW_SENSITIVE";

    public override object ParameterSchema => new { type = "OBJECT", properties = new Dictionary<string, object>() };

    private static readonly string[] BandOrder = ["intern", "trainee", "associate", "junior", "mid", "senior", "lead", "staff", "principal", "manager", "director"];

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN", "FINANCE"))
                return new ToolResult(false, null, "RBAC: the compensation matrix is limited to HR, Finance and Admin roles.");

            var identity = Identity();
            var people = (await EmployeeDirectory.LoadAsync(identity, cancellationToken))
                .Where(p => p.Status is null or "ACTIVE" or "ON_LEAVE").ToList();
            if (people.Count == 0)
                return new ToolResult(false, null, "I could not read the employee directory, so no bands could be built.");

            var refs = await EmployeeDirectory.ReferencesAsync(identity, cancellationToken);
            var considered = people.Take(MaxPeople).ToList();
            var gate = new SemaphoreSlim(6);
            var today = TodayLocal;

            async Task<(DirectoryEntry Person, decimal Ctc, decimal Bonus, string Currency)?> Load(DirectoryEntry person)
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    using var doc = await TryGetAsync(identity, IdentityRoutes.EmployeeCompensation(person.EmployeeId), cancellationToken);
                    if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
                    
                    JsonElement? best = null;
                    foreach (var row in doc.RootElement.EnumerateArray())
                    {
                        var from = DateOnly.TryParse(StrOf(row, "effectiveFrom"), CultureInfo.InvariantCulture, out var f) ? f : DateOnly.MinValue;
                        var to = DateOnly.TryParse(StrOf(row, "effectiveTo"), CultureInfo.InvariantCulture, out var t) ? t : DateOnly.MaxValue;
                        if (from <= today && today <= to) { best = row.Clone(); break; }
                    }

                    if (best == null) return null;
                    var ctc = NumProp(best.Value, "ctc");
                    if (ctc == null) return null;
                    return (person, (decimal)ctc.Value, (decimal)(NumProp(best.Value, "bonus") ?? 0), StrOf(best.Value, "currency") ?? "INR");
                }
                finally { gate.Release(); }
            }

            var loaded = (await Task.WhenAll(considered.Select(Load))).Where(x => x.HasValue).Select(x => x!.Value).ToList();
            if (loaded.Count == 0)
                return new ToolResult(false, null, "No current compensation records were found, or your role cannot read them.");

            string BandOf(DirectoryEntry p) =>
                p.DesignationId is { } d && refs.TryGetValue(d, out var dr) ? dr.Name
                : p.GradeId is { } g && refs.TryGetValue(g, out var gr) ? gr.Name
                : "Unclassified";

            int Rank(string band)
            {
                var lower = band.ToLowerInvariant();
                for (var i = 0; i < BandOrder.Length; i++) if (lower.Contains(BandOrder[i])) return i;
                return BandOrder.Length;
            }

            var bands = loaded
                .GroupBy(x => (Band: BandOf(x.Person), x.Currency))
                .OrderBy(g => Rank(g.Key.Band)).ThenBy(g => g.Key.Band).ThenBy(g => g.Key.Currency)
                .Select(g => new
                {
                    band = g.Key.Band,
                    currency = g.Key.Currency,
                    headcount = g.Count(),
                    basedOn = g.Count() == 1 ? "1 compensation record: these figures are that one person's pay, not a spread" : $"{g.Count()} compensation records",
                    ctcMin = Math.Round(g.Min(x => x.Ctc), 0),
                    ctcAverage = Math.Round(g.Average(x => x.Ctc), 0),
                    ctcMax = Math.Round(g.Max(x => x.Ctc), 0),
                    bonusAverage = Math.Round(g.Average(x => x.Bonus), 0),
                    bonusPercentOfCtc = Math.Round(g.Where(x => x.Ctc > 0).Select(x => x.Bonus / x.Ctc * 100).DefaultIfEmpty(0).Average(), 1)
                })
                .ToArray();

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                asOf = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                employeesInDirectory = people.Count,
                employeesWithCurrentCompensation = loaded.Count,
                employeesWithoutCompensation = considered.Count - loaded.Count,
                limitedToFirst = people.Count > MaxPeople ? MaxPeople : (int?)null,
                bands,
                note = "This is an organisation-wide summary by designation, built only from people who have a current compensation record. It says nothing about any named person: never tell the user it is someone's band, and for one person's pay point them to that employee's Compensation tab. People without a record are not in it."
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class GetEmployeeCompensationTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "get_employee_compensation";
    public override string Description => "Show one named employee's compensation records: current CTC, bonus and currency, and earlier periods. Only HR and Admin may use it; other roles are refused. Pass the person's name or employee code in 'query', or an employeeId.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW_SENSITIVE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "A name or employee code." },
            ["employeeId"] = new { type = "INTEGER", description = "Employee record id, when known from an earlier result." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN"))
                return new ToolResult(false, null, "RBAC: an individual's compensation can only be viewed by HR and Admin roles.");

            var identity = Identity();
            var (profile, choices, error) = await PersonaProfiles.ResolveAsync(identity, Long(arguments, "employeeId"), Str(arguments, "query"), cancellationToken);
            if (error != null) return new ToolResult(false, null, error);
            if (choices != null)
                return new ToolResult(true, JsonSerializer.Serialize(new { needsChoice = true, message = "More than one person matches. Ask which one is meant.", candidates = choices }));

            string name, code;
            long id;
            using (profile)
            {
                var p = profile!.RootElement;
                id = p.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var i) ? i : 0;
                name = $"{StrOf(p, "firstName")} {StrOf(p, "lastName")}".Trim();
                code = StrOf(p, "employeeCode") ?? "";
            }

            using var response = await identity.GetAsync(IdentityRoutes.EmployeeCompensation(id), cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return new ToolResult(false, null, "RBAC: your account is not allowed to view compensation records.");
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"The employee service answered HTTP {(int)response.StatusCode}.");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var today = TodayLocal;
            var records = new List<(DateOnly From, DateOnly? To, double Ctc, double Bonus, string Currency)>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    if (!DateOnly.TryParse(StrOf(row, "effectiveFrom"), CultureInfo.InvariantCulture, out var from)) continue;
                    DateOnly? to = DateOnly.TryParse(StrOf(row, "effectiveTo"), CultureInfo.InvariantCulture, out var t) ? t : null;
                    records.Add((from, to, NumProp(row, "ctc") ?? 0, NumProp(row, "bonus") ?? 0, StrOf(row, "currency") ?? "INR"));
                }

            if (records.Count == 0)
                return new ToolResult(true, JsonSerializer.Serialize(new { employee = name, employeeCode = code, recorded = false, message = $"No compensation has been recorded for {name} yet." }));

            object Shape((DateOnly From, DateOnly? To, double Ctc, double Bonus, string Currency) r) => new
            {
                effectiveFrom = r.From,
                effectiveTo = r.To,
                ctc = r.Ctc,
                bonus = r.Bonus,
                currency = r.Currency
            };

            var ordered = records.OrderByDescending(r => r.From).ToList();
            var current = ordered.FirstOrDefault(r => r.From <= today && (r.To == null || today <= r.To));
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                employee = name,
                employeeCode = code,
                recorded = true,
                current = current == default ? null : Shape(current),
                note = current == default ? "No record covers today; the latest ones are listed under history." : null,
                history = ordered.Select(Shape).ToArray()
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}





public class PrepareDeleteTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    private static readonly string[] Kinds = ["worklog", "leave", "skills", "education", "certifications", "experience", "languages", "comments"];

    public override string Name => "prepare_delete";
    public override string Description => "Open the Delete form in the chat so the user can delete their OWN entries: time logs (work logs), leave requests (cancel a pending one), skills, education, certifications, work experience, languages, or their comments on an issue. It lists the entries with a Delete button each, and asks again before deleting. Call it whenever the user wants to delete or remove one of those. Pass 'kind' if they named it, and 'issue' (an issue key) for comments. You cannot delete anything yourself and must never say that something was deleted. Issues, projects, accounts, other people's entries and chats are not covered: say so.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["kind"] = new { type = "STRING", description = "worklog, leave, skills, education, certifications, experience, languages or comments, only if the user named it.", @enum = Kinds },
            ["issue"] = new { type = "STRING", description = "An issue key such as FLP360-17, for comments." }
        }
    };

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var kind = (Str(arguments, "kind") ?? "").Trim().ToLowerInvariant();
        if (!Kinds.Contains(kind)) kind = "";
        var issue = (Str(arguments, "issue") ?? "").Trim().ToUpperInvariant();
        return Task.FromResult(new ToolResult(true, JsonSerializer.Serialize(new { form = "delete", kind, issueKey = issue })));
    }
}




public class GetEmployeeFullDetailsTool : ServiceToolBase
{
    private readonly IHttpClientFactory _clients;
    private readonly IHttpContextAccessor _context;

    public GetEmployeeFullDetailsTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : base(factory, accessor)
    {
        _clients = factory;
        _context = accessor;
    }

    private static readonly string[] Records = ["personal", "experience", "education", "certifications", "languages"];

    public override string Name => "get_employee_full_details";
    public override string Description => "Get EVERYTHING about one employee in a single call, for HR and Admin only: profile (job, manager, reporting chain, direct reports, projects), personal details, career history, skills, experience, education, certifications, languages and compensation. Use it when HR or Admin asks for 'all details', 'full details', 'everything' or 'the complete profile' of a person. Pass a name or employee code in 'query'. For one tab only, use the tab's own tool instead.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW_SENSITIVE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "A name or employee code. Omit for the signed-in user." },
            ["employeeId"] = new { type = "INTEGER", description = "Employee record id, when known from an earlier result." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN"))
                return new ToolResult(false, null, "RBAC: the full details of an employee are limited to HR and Admin roles. " + PersonaProfiles.WholeProfileOnlyMessage);

            var (profile, choices, error) = await PersonaProfiles.ResolveAsync(Identity(), Long(arguments, "employeeId"), Str(arguments, "query"), cancellationToken);
            if (error != null) return new ToolResult(false, null, error);
            if (choices != null)
                return new ToolResult(true, JsonSerializer.Serialize(new { needsChoice = true, message = "More than one person matches. Ask which one is meant.", candidates = choices }));

            long id;
            using (profile)
                id = profile!.RootElement.TryGetProperty("id", out var idValue) && idValue.TryGetInt64(out var parsed) ? parsed : 0;

            
            Dictionary<string, object?> For(string? section = null)
            {
                var copy = new Dictionary<string, object?>(arguments) { ["employeeId"] = id };
                copy.Remove("query");
                if (section != null) copy["section"] = section;
                return copy;
            }

            var parts = new List<(string Key, Task<ToolResult> Result)>
            {
                ("profile", new GetEmployeeProfileTool(_clients, _context).ExecuteAsync(For(), cancellationToken)),
                ("careerHistory", new GetCareerHistoryTool(_clients, _context).ExecuteAsync(For(), cancellationToken)),
                ("compensation", new GetEmployeeCompensationTool(_clients, _context).ExecuteAsync(For(), cancellationToken))
            };
            foreach (var section in Records)
                parts.Add((section, new GetEmployeeRecordTool(_clients, _context).ExecuteAsync(For(section), cancellationToken)));
            await Task.WhenAll(parts.Select(p => p.Result));

            var result = new Dictionary<string, object?> { ["employeeId"] = id };
            var missing = new List<string>();
            foreach (var (key, task) in parts)
            {
                var part = task.Result;
                if (!part.Success || part.Data == null)
                {
                    result[key] = null;
                    missing.Add($"{key}: {part.Error ?? "could not be read"}");
                    continue;
                }

                using var doc = JsonDocument.Parse(part.Data as string ?? JsonSerializer.Serialize(part.Data));
                var root = doc.RootElement;
                
                if (key == "profile") result["employee"] = root.Clone();
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    var trimmed = new Dictionary<string, JsonElement>();
                    foreach (var property in root.EnumerateObject())
                        if (property.Name is not ("employee" or "employeeCode" or "section")) trimmed[property.Name] = property.Value.Clone();
                    result[key] = trimmed;
                }
                else result[key] = root.Clone();
            }

            result["unavailable"] = missing;
            result["note"] = missing.Count == 0
                ? "Everything below was read in one call. Show every section, one table each, and write 'none recorded' for an empty one."
                : "Some parts could not be read; list them plainly under 'unavailable' and show the rest. Do not claim a missing part is empty.";
            return new ToolResult(true, JsonSerializer.Serialize(result));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}




public class GetAllEmployeesDetailsTool : ServiceToolBase
{
    private const int MaxEmployees = 500;
    private const int AtOnce = 6;

    private readonly IHttpClientFactory _clients;
    private readonly IHttpContextAccessor _context;

    public GetAllEmployeesDetailsTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : base(factory, accessor)
    {
        _clients = factory;
        _context = accessor;
    }

    public override string Name => "get_all_employees_details";
    public override string Description => "Get the details of EVERY employee in one call, for HR and Admin only: for each person the code, name, work email, phone, designation, job title, department, grade, location, employment type, status, joining date, manager, skills, experience, education, certifications, languages and current compensation. Use it when HR or Admin asks for 'all employees', 'all the employee details', 'every employee' or 'the whole directory'. It also prepares an Excel (.xlsx) file with everything for download. For one named person use get_employee_full_details instead.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_VIEW_SENSITIVE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["status"] = new { type = "STRING", description = "Only people with this employment status, for example ACTIVE. Leave out for everyone." }
        }
    };

    private sealed record Person(
        long Id, string Code, string Name, string? Email, string? Phone, string? Designation, string? JobTitle, string? Department,
        string? Grade, string? Location, string? Type, string? Status, string? Joined, string? Manager,
        string Skills, string Experience, string Education, string Certifications, string Languages,
        int[] Counts, string? Ctc, string? Bonus, string? Currency, List<string> Unavailable);

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN"))
                return new ToolResult(false, null, "RBAC: the details of all employees are limited to HR and Admin roles.");

            var identity = Identity();
            var status = (Str(arguments, "status") ?? "").Trim().ToUpperInvariant();

            
            
            var records = new List<JsonElement>();
            var batch = false;
            using (var export = await TryGetAsync(identity, $"{IdentityRoutes.ExportDetails}?limit={MaxEmployees}" + (status.Length > 0 ? $"&status={Uri.EscapeDataString(status)}" : ""), cancellationToken))
            {
                if (export != null && export.RootElement.ValueKind == JsonValueKind.Object && export.RootElement.TryGetProperty("items", out var batchItems) && batchItems.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in batchItems.EnumerateArray()) records.Add(item.Clone());
                    batch = true;
                }
            }

            if (!batch)
            {
                for (var page = 1; records.Count < MaxEmployees; page++)
                {
                    var url = $"{IdentityRoutes.Employees}?Page={page}&PageSize=100" + (status.Length > 0 ? $"&EmploymentStatus={Uri.EscapeDataString(status)}" : "");
                    using var list = await TryGetAsync(identity, url, cancellationToken);
                    if (list == null || list.RootElement.ValueKind != JsonValueKind.Object || !list.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                    {
                        if (page == 1) return new ToolResult(false, null, "The employee directory could not be read. You may not be allowed to view it.");
                        break;
                    }

                    foreach (var item in items.EnumerateArray()) records.Add(item.Clone());
                    var totalPages = list.RootElement.TryGetProperty("totalPages", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetInt32() : 1;
                    if (page >= totalPages || items.GetArrayLength() == 0) break;
                }
            }

            var refs = await EmployeeDirectory.ReferencesAsync(identity, cancellationToken);
            string? Ref(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var id) && refs.TryGetValue(id, out var r) ? r.Name : null;
            string? Date(JsonElement e, string name) => StrOf(e, name) is { Length: >= 10 } d ? d[..10] : null;
            string FullName(JsonElement e) => $"{StrOf(e, "firstName")} {StrOf(e, "lastName")}".Trim();

            var managers = records
                .Where(e => e.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.Number)
                .ToDictionary(e => e.GetProperty("id").GetInt64(), FullName);

            var scale = new[] { "", "Beginner", "Basic", "Intermediate", "Advanced", "Expert" };
            string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            string Span(JsonElement x) => $"{Date(x, "startDate") ?? "?"} to {Date(x, "endDate") ?? "present"}";
            string SkillText(JsonElement s) => $"{StrOf(s, "skillName")} ({(int.TryParse(StrOf(s, "proficiency"), out var p) && p is >= 1 and <= 5 ? scale[p] : "level not set")}, {StrOf(s, "yearsOfExperience") ?? "0"} yrs)";
            string ExperienceText(JsonElement x) => $"{StrOf(x, "title")} at {StrOf(x, "company")} ({Span(x)})";
            string EducationText(JsonElement x) => $"{StrOf(x, "qualification")}, {StrOf(x, "fieldOfStudy")}, {StrOf(x, "institution")} ({Span(x)})";
            string CertificationText(JsonElement x) => $"{StrOf(x, "name")} - {StrOf(x, "issuer")}";
            string LanguageText(JsonElement x) => $"{StrOf(x, "name")} ({StrOf(x, "proficiency")})";
            string JoinAll(JsonElement list, Func<JsonElement, string> text) => string.Join("; ", list.EnumerateArray().Select(text).Where(t => t.Length > 0));
            var today = DateOnly.FromDateTime(DateTime.Now);
            (string? Ctc, string? Bonus, string? Currency) CurrentPay(JsonElement list)
            {
                var current = list.EnumerateArray()
                    .Where(r => DateOnly.TryParse(StrOf(r, "effectiveFrom"), CultureInfo.InvariantCulture, out var from) && from <= today
                                && (!DateOnly.TryParse(StrOf(r, "effectiveTo"), CultureInfo.InvariantCulture, out var to) || today <= to))
                    .OrderByDescending(r => StrOf(r, "effectiveFrom"))
                    .Cast<JsonElement?>().FirstOrDefault();
                return current is { } c ? (StrOf(c, "ctc"), StrOf(c, "bonus"), StrOf(c, "currency")) : (null, null, null);
            }
            string? Manager(JsonElement e, long id) =>
                e.TryGetProperty("managerEmployeeId", out var m) && m.ValueKind == JsonValueKind.Number && m.GetInt64() is var managerId && managerId != id && managers.TryGetValue(managerId, out var mgr) ? mgr : null;

            
            Person FromBatch(JsonElement e)
            {
                var id = e.GetProperty("id").GetInt64();
                JsonElement Part(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v : JsonDocument.Parse("[]").RootElement;
                var career = e.TryGetProperty("career", out var c) && c.ValueKind == JsonValueKind.Object ? c : (JsonElement?)null;
                string? Snapshot(string name) => career is { } l ? StrOf(l, name) : null;
                var (ctc, bonus, currency) = CurrentPay(Part("compensation"));
                return new Person(
                    id, StrOf(e, "employeeCode") ?? "", FullName(e), StrOf(e, "workEmail"), Clean(StrOf(e, "phone")),
                    Ref(e, "designationId") ?? Clean(Snapshot("designation")), Ref(e, "jobTitleId") ?? Clean(Snapshot("jobTitle")),
                    Ref(e, "departmentId") ?? Clean(Snapshot("department")), Ref(e, "gradeId") ?? Clean(Snapshot("grade")), Ref(e, "locationId"),
                    StrOf(e, "employmentType"), StrOf(e, "employmentStatus"), Date(e, "joiningDate"), Manager(e, id),
                    JoinAll(Part("skills"), SkillText), JoinAll(Part("experience"), ExperienceText), JoinAll(Part("education"), EducationText),
                    JoinAll(Part("certifications"), CertificationText), JoinAll(Part("languages"), LanguageText),
                    [Part("skills").GetArrayLength(), Part("experience").GetArrayLength(), Part("education").GetArrayLength(), Part("certifications").GetArrayLength(), Part("languages").GetArrayLength()],
                    ctc, bonus, currency, []);
            }

            using var gate = new SemaphoreSlim(AtOnce);

            async Task<Person> Read(JsonElement e)
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var id = e.GetProperty("id").GetInt64();
                    var unavailable = new List<string>();

                    async Task<JsonDocument?> Section(string name)
                    {
                        var doc = await TryGetAsync(identity, IdentityRoutes.EmployeeSection(id, name), cancellationToken);
                        if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) { doc?.Dispose(); unavailable.Add(name); return null; }
                        return doc;
                    }

                    string Join(JsonDocument? doc, Func<JsonElement, string> text) => doc == null ? "" : JoinAll(doc.RootElement, text);
                    int Count(JsonDocument? doc) => doc?.RootElement.GetArrayLength() ?? 0;

                    using var skills = await Section("skills");
                    using var experience = await Section("experience");
                    using var education = await Section("education");
                    using var certifications = await Section("certifications");
                    using var languages = await Section("languages");
                    using var compensation = await Section("compensation");

                    
                    
                    using var full = await TryGetAsync(identity, IdentityRoutes.Employee(id), cancellationToken);
                    using var career = await TryGetAsync(identity, IdentityRoutes.EmployeeCareerHistory(id), cancellationToken);
                    JsonElement? latest = career is { RootElement.ValueKind: JsonValueKind.Array } && career.RootElement.GetArrayLength() > 0
                        ? career.RootElement.EnumerateArray().OrderByDescending(x => x.TryGetProperty("id", out var cid) && cid.TryGetInt64(out var n) ? n : 0).First()
                        : null;
                    string? Snapshot(string name) => latest is { } l && l.TryGetProperty("details", out var d) ? StrOf(d, name) : null;

                    var (ctc, bonus, currency) = compensation != null ? CurrentPay(compensation.RootElement) : (null, null, null);

                    return new Person(
                        id, StrOf(e, "employeeCode") ?? "", FullName(e), StrOf(e, "workEmail"), Clean(full != null ? StrOf(full.RootElement, "phone") : null) ?? Clean(StrOf(e, "phone")),
                        Ref(e, "designationId") ?? Clean(Snapshot("designation")), Ref(e, "jobTitleId") ?? Clean(Snapshot("jobTitle")),
                        Ref(e, "departmentId") ?? Clean(Snapshot("department")), Ref(e, "gradeId") ?? Clean(Snapshot("grade")), Ref(e, "locationId"),
                        StrOf(e, "employmentType"), StrOf(e, "employmentStatus"), Date(e, "joiningDate"),
                        Manager(e, id),
                        Join(skills, SkillText), Join(experience, ExperienceText), Join(education, EducationText),
                        Join(certifications, CertificationText), Join(languages, LanguageText),
                        [Count(skills), Count(experience), Count(education), Count(certifications), Count(languages)],
                        ctc, bonus, currency, unavailable);
                }
                finally { gate.Release(); }
            }

            var people = (await Task.WhenAll(records.Where(e => e.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.Number).Select(e => batch ? Task.FromResult(FromBatch(e)) : Read(e))))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

            
            
            (string Header, int Width, string Type, string Group)[] columns =
            [
                ("Employee ID", 15, "code", "Identity"), ("Employee Name", 28, "strong", "Identity"), ("Work Email", 36, "text", "Identity"), ("Phone", 17, "text", "Identity"),
                ("Designation", 21, "tag", "Job"), ("Job Title", 31, "tag", "Job"), ("Department", 24, "tag", "Job"), ("Grade", 10, "tag", "Job"),
                ("Location", 17, "tag", "Employment"), ("Employment Type", 17, "tag", "Employment"), ("Status", 13, "status", "Employment"),
                ("Joining Date", 14, "text", "Employment"), ("Manager", 26, "text", "Employment"),
                ("Skills", 50, "list", "Profile"), ("Experience", 56, "list", "Profile"), ("Education", 48, "list", "Profile"),
                ("Certifications", 42, "list", "Profile"), ("Languages", 26, "list", "Profile"),
                ("Current CTC", 17, "number", "Compensation"), ("Bonus", 15, "number", "Compensation"), ("Currency", 11, "tag", "Compensation")
            ];
            static object? Number(string? text) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
            static string? Words(string? text) => string.IsNullOrWhiteSpace(text) ? text : text.Replace('_', ' ');
            
            static string? Lines(string? text) => string.IsNullOrWhiteSpace(text) ? null : string.Join("\n", text.Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(x => "• " + x));
            var table = people.Select(p => new object?[]
            {
                string.IsNullOrEmpty(p.Code) ? "" : "#" + p.Code, p.Name, p.Email, p.Phone, p.Designation, p.JobTitle, p.Department, p.Grade, p.Location,
                Words(p.Type), p.Status, p.Joined, p.Manager, Lines(p.Skills), Lines(p.Experience), Lines(p.Education), Lines(p.Certifications), Lines(p.Languages),
                Number(p.Ctc), Number(p.Bonus), p.Currency
            }).ToList();
            var sheet = new
            {
                title = "Employee Details Report · " + (status.Length > 0 ? status[..1] + status[1..].ToLowerInvariant() + " Employees" : "All Employees"),
                subtitle = $"Prepared by Athenna on {TodayLocal.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)} · confidential: HR and Admin only",
                sheetName = "Employees",
                summary = new[]
                {
                    new { label = "Total Employees", value = people.Count.ToString(CultureInfo.InvariantCulture) },
                    new { label = "Active", value = people.Count(p => string.Equals(p.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture) },
                    new { label = "Departments", value = people.Select(p => p.Department).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().Count().ToString(CultureInfo.InvariantCulture) },
                    new { label = "Export Date", value = TodayLocal.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) }
                },
                columns = columns.Select(c => new { header = c.Header, width = c.Width, type = c.Type, group = c.Group }).ToArray(),
                rows = table,
                breakdowns = new[] { "Department", "Designation", "Grade", "Employment Type", "Status", "Location" }
            };

            var rows = people.Select(p => new
            {
                employeeCode = p.Code,
                name = p.Name,
                workEmail = p.Email,
                designation = p.Designation,
                jobTitle = p.JobTitle,
                department = p.Department,
                grade = p.Grade,
                location = p.Location,
                employmentType = p.Type,
                status = p.Status,
                joiningDate = p.Joined,
                manager = p.Manager,
                skills = p.Skills,
                counts = new { skills = p.Counts[0], experience = p.Counts[1], education = p.Counts[2], certifications = p.Counts[3], languages = p.Counts[4] },
                currentCtc = p.Ctc == null ? null : $"{p.Ctc} {p.Currency}".Trim(),
                partsNotRead = p.Unavailable.Count == 0 ? null : string.Join(", ", p.Unavailable)
            }).ToList();

            var truncated = records.Count >= MaxEmployees;
            var fileName = $"All_Employees_Report{(status.Length > 0 ? "_" + status[..1] + status[1..].ToLowerInvariant() : "")}_{TodayLocal:yyyy-MM-dd}.xlsx";
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                count = rows.Count,
                filter = status.Length > 0 ? status : "all statuses",
                message = rows.Count == 0 ? "There are no employees to show." : truncated ? $"Only the first {MaxEmployees} employees are included." : null,
                employees = rows,
                file = new { fileName, mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", format = "table", content = JsonSerializer.Serialize(sheet) }
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}

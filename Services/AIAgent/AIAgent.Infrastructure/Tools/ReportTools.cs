using System.Globalization;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;





public abstract class ReportToolBase(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    protected const int PageSize = 100;
    protected const int MaxPages = 5;
    public const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    protected static ToolResult Fail(string message) => new(false, null, message);

    protected static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected static string Pretty(DateOnly date) => date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

    protected static string Words(string? code) => string.IsNullOrWhiteSpace(code) ? "" : code.Replace('_', ' ').Trim();

    
    protected static string Sentence(string? code)
    {
        var text = Words(code).ToLowerInvariant();
        return text.Length == 0 ? "" : char.ToUpperInvariant(text[0]) + text[1..];
    }

    protected static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    
    protected static string FilePart(string? text) => new string((text ?? "").Where(char.IsLetterOrDigit).ToArray());

    protected static object Column(string header, int width, string type, string group, bool decimals = false) =>
        decimals ? new { header, width, type, group, decimals } : new { header, width, type, group, decimals = (bool?)null };

    protected static object File(string fileName, object sheet) =>
        new { fileName, mimeType = XlsxMime, format = "table", content = JsonSerializer.Serialize(sheet) };

    
    protected static async Task<(List<JsonElement> Items, long Total, string? Error)> ReadPagesAsync(HttpClient client, Func<int, string> url, string what, CancellationToken ct)
    {
        var items = new List<JsonElement>();
        long total = 0;
        for (var page = 1; page <= MaxPages; page++)
        {
            using var response = await client.GetAsync(url(page), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return (items, total, $"{what}: {ApiMessage(body, (int)response.StatusCode)}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var batch = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Select(x => x.Clone()).ToList()
                : [];
            items.AddRange(batch);
            total = LongProp(root, "totalCount") ?? items.Count;
            if (batch.Count < PageSize || items.Count >= total) break;
        }
        return (items, total, null);
    }
}



public class ExportIssuesReportTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ReportToolBase(factory, accessor)
{
    public override string Name => "export_issues_report";
    public override string Description =>
        "Prepare an Excel (.xlsx) report of issues for download: key, summary, type, status, priority, assignee, reporter, due date, how overdue, story points and estimated, logged and remaining hours, with a summary sheet. " +
        "Use it when the user asks for an issues report, an Excel or spreadsheet of issues, 'export my issues', 'download the open bugs' or 'a report of project FLP360'. Pass the filters the user named (project, status, priority, assignee, dates, openOnly, overdueOnly) and leave the rest out. " +
        "Developers always get only the issues assigned to them. It reads up to 500 issues. The file is shown to the user as a download below your message: say how many issues it holds from the totals, never print the file contents, and do not list the issues again.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["project"] = new { type = "STRING", description = "A project name or key the user named ('FLP360'). Leave out for every project the person can see." },
            ["statusCode"] = new { type = "STRING", description = "One status: TODO, IN_PROGRESS, TESTING, DONE." },
            ["priority"] = new { type = "STRING", description = "One priority: CRITICAL, HIGH, MEDIUM, LOW." },
            ["assignee"] = new { type = "STRING", description = "A person's name, or 'me'. Managers and admins only; developers always get their own." },
            ["query"] = new { type = "STRING", description = "Keyword the issue text must contain." },
            ["dueFrom"] = new { type = "STRING", description = "Only issues due on or after this date, YYYY-MM-DD." },
            ["dueTo"] = new { type = "STRING", description = "Only issues due on or before this date, YYYY-MM-DD." },
            ["openOnly"] = new { type = "BOOLEAN", description = "True to leave out issues that are done or cancelled." },
            ["overdueOnly"] = new { type = "BOOLEAN", description = "True for only the open issues whose due date has passed." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;
            var caller = CallerUserId(arguments);

            var jira = Jira();
            var today = TodayLocal;

            
            if (Str(arguments, "dueFrom") is { } f && !TryDate(f, out _)) return Fail("dueFrom must be written as YYYY-MM-DD.");
            if (Str(arguments, "dueTo") is { } t && !TryDate(t, out _)) return Fail("dueTo must be written as YYYY-MM-DD.");
            TryDate(Str(arguments, "dueFrom"), out var dueFrom);
            var hasFrom = Str(arguments, "dueFrom") != null;
            TryDate(Str(arguments, "dueTo"), out var dueTo);
            var hasTo = Str(arguments, "dueTo") != null;
            if (hasFrom && hasTo && dueTo < dueFrom) return Fail("dueFrom is after dueTo.");

            
            SprintProject? project = null;
            if (Str(arguments, "project") is { } named)
            {
                using var projectDoc = await TryGetAsync(jira, owner.HasValue ? JiraRoutes.ProjectsByUser(owner.Value) : JiraRoutes.Projects, cancellationToken);
                if (projectDoc == null || projectDoc.RootElement.ValueKind != JsonValueKind.Array) return Fail("I could not list your projects right now.");
                var projects = projectDoc.RootElement.EnumerateArray()
                    .Where(p => LongProp(p, "id").HasValue)
                    .Select(p => new SprintProject(LongProp(p, "id")!.Value, Text(p, "key"),
                        new[] { Text(p, "displayName"), Text(p, "projectName"), Text(p, "name") }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))))
                    .ToList();
                var matches = GetSprintOverviewTool.FindProjects(named, projects);
                if (matches.Count == 0) return Fail($"No project matches '{named}'. Your projects: {string.Join(", ", projects.Take(15).Select(p => p.Key ?? p.Name))}.");
                if (matches.Count > 1) return Fail($"'{named}' fits more than one project: {string.Join(", ", matches.Take(8).Select(p => $"{p.Name} ({p.Key})"))}. Ask the user which one.");
                project = matches[0];
            }

            
            var identity = Identity();
            var people = await EmployeeDirectory.LoadAsync(identity, cancellationToken);
            long? assigneeId = null;
            string? assigneeName = null;
            var said = Str(arguments, "assignee");
            if (owner.HasValue)
            {
                assigneeId = owner;
                if (said != null && !IsMe(said) && !people.Any(p => p.UserId == owner && string.Equals(p.Name, said, StringComparison.OrdinalIgnoreCase)))
                    return Fail("You can only export issues assigned to you.");
            }
            else if (said != null)
            {
                if (IsMe(said))
                {
                    if (caller is null or <= 0) return Fail("I can't tell who you are. Please sign in again.");
                    assigneeId = caller;
                }
                else
                {
                    var pool = people.Where(p => p.UserId > 0).Select(p => new AssigneeCandidate(p.UserId, p.Name, p.Email)).ToList();
                    var found = PersonMatch.Find(said, pool);
                    if (found.Count == 0) return Fail($"No one is called '{said}'.");
                    if (found.Count > 1) return Fail($"'{said}' fits more than one person: {string.Join(", ", found.Take(8).Select(p => p.Name))}. Ask the user which one.");
                    assigneeId = found[0].UserId;
                }
            }
            if (assigneeId.HasValue) assigneeName = people.FirstOrDefault(p => p.UserId == assigneeId)?.Name;

            
            string Url(int page)
            {
                var u = $"{JiraRoutes.IssueSearch}?Page={page}&PageSize={PageSize}&SortDirection=desc";
                if (project != null) u += $"&ProjectId={project.Id}";
                if (assigneeId.HasValue) u += $"&AssigneeUserId={assigneeId.Value}";
                if (Str(arguments, "statusCode") is { } status) u += $"&Status={Uri.EscapeDataString(status.ToUpperInvariant())}";
                if (Str(arguments, "priority") is { } priority) u += $"&Priority={Uri.EscapeDataString(priority.ToUpperInvariant())}";
                if (Str(arguments, "query") is { } query && query != "*") u += $"&Text={Uri.EscapeDataString(query)}";
                if (hasFrom) u += $"&DueFrom={Day(dueFrom)}";
                if (hasTo) u += $"&DueTo={Day(dueTo)}";
                return u;
            }
            var (read, total, error) = await ReadPagesAsync(jira, Url, "I could not read the issues", cancellationToken);
            if (error != null) return Fail(error);

            bool Closed(JsonElement i) => Text(i, "statusCode") is "DONE" or "CANCELLED";
            DateOnly? Due(JsonElement i) => DateOnly.TryParse(Text(i, "dueDate"), CultureInfo.InvariantCulture, out var d) ? d : null;
            var issues = read.Where(i => owner is not { } o || AssignedTo(i, o)).ToList();
            if (Bool(arguments, "openOnly", false)) issues = issues.Where(i => !Closed(i)).ToList();
            if (Bool(arguments, "overdueOnly", false)) issues = issues.Where(i => !Closed(i) && Due(i) is { } d && d < today).ToList();
            issues = issues
                .OrderBy(i => Text(i, "issueKey") is { } k && k.LastIndexOf('-') > 0 ? k[..k.LastIndexOf('-')] : "", StringComparer.Ordinal)
                .ThenBy(i => LongProp(i, "issueNumber") ?? long.MaxValue)
                .ThenBy(i => Text(i, "issueKey"), StringComparer.Ordinal)
                .ToList();

            if (issues.Count == 0)
                return new ToolResult(true, JsonSerializer.Serialize(new { count = 0, note = "No issues match, so no file was prepared." }));

            
            string Names(JsonElement i)
            {
                var ids = new List<long>();
                if (i.TryGetProperty("assigneeUserIds", out var many) && many.ValueKind == JsonValueKind.Array)
                    ids.AddRange(many.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.Number).Select(a => a.GetInt64()));
                if (LongProp(i, "assigneeUserId") is { } one && !ids.Contains(one)) ids.Insert(0, one);
                return ids.Count == 0 ? "Unassigned" : string.Join(", ", ids.Select(id => EmployeeDirectory.NameOfUser(people, id) ?? $"User {id}"));
            }
            string Late(JsonElement i) => !Closed(i) && Due(i) is { } d && d < today ? $"{Plural(today.DayNumber - d.DayNumber, "day")} late" : "";
            double H(JsonElement i, string name) => Math.Round((LongProp(i, name) ?? 0) / 60.0, 2);

            var rows = issues.Select(i => new object?[]
            {
                Text(i, "issueKey"), Text(i, "summary"), Sentence(Text(i, "issueType")), Words(Text(i, "statusCode")), Sentence(Text(i, "priority")),
                Names(i), LongProp(i, "reporterUserId") is { } r ? EmployeeDirectory.NameOfUser(people, r) ?? $"User {r}" : "",
                Text(i, "dueDate") ?? "", Late(i),
                LongProp(i, "storyPoints"), H(i, "originalEstimateMinutes"), H(i, "loggedTimeMinutes"), H(i, "remainingEstimateMinutes")
            }).ToList();

            var open = issues.Count(i => !Closed(i));
            var done = issues.Count(i => Text(i, "statusCode") == "DONE");
            var overdue = issues.Count(i => Late(i).Length > 0);
            var points = issues.Sum(i => LongProp(i, "storyPoints") ?? 0);
            var logged = Math.Round(issues.Sum(i => H(i, "loggedTimeMinutes")), 2);

            var filters = new List<string>();
            if (project != null) filters.Add($"project {project.Name ?? project.Key}");
            if (assigneeId.HasValue) filters.Add($"assigned to {assigneeName ?? "you"}");
            if (Str(arguments, "statusCode") is { } sc) filters.Add($"status {Words(sc).ToLowerInvariant()}");
            if (Str(arguments, "priority") is { } pr) filters.Add($"priority {pr.ToLowerInvariant()}");
            if (Str(arguments, "query") is { } q && q != "*") filters.Add($"matching \"{q}\"");
            if (hasFrom) filters.Add($"due from {Day(dueFrom)}");
            if (hasTo) filters.Add($"due to {Day(dueTo)}");
            if (Bool(arguments, "overdueOnly", false)) filters.Add("overdue only");
            else if (Bool(arguments, "openOnly", false)) filters.Add("open only");
            var filterText = filters.Count == 0 ? "all issues you can see" : string.Join(", ", filters);

            var sheet = new
            {
                title = "Issues Report" + (project != null ? $" · {project.Name ?? project.Key}" : ""),
                subtitle = $"Prepared by Athenna on {Pretty(today)} · {filterText}",
                sheetName = "Issues",
                summary = new[]
                {
                    new { label = "Issues", value = issues.Count.ToString(CultureInfo.InvariantCulture) },
                    new { label = "Open", value = open.ToString(CultureInfo.InvariantCulture) },
                    new { label = "Overdue", value = overdue.ToString(CultureInfo.InvariantCulture) },
                    new { label = "Logged hours", value = Number(logged) }
                },
                columns = new[]
                {
                    Column("Key", 14, "code", "Issue"), Column("Summary", 52, "text", "Issue"), Column("Type", 12, "tag", "Issue"),
                    Column("Status", 15, "status", "Issue"), Column("Priority", 12, "tag", "Issue"),
                    Column("Assignee", 26, "text", "People"), Column("Reporter", 24, "text", "People"),
                    Column("Due date", 13, "text", "Schedule"), Column("Overdue", 14, "text", "Schedule"),
                    Column("Story points", 12, "number", "Effort"), Column("Estimate (h)", 13, "number", "Effort", true),
                    Column("Logged (h)", 12, "number", "Effort", true), Column("Remaining (h)", 14, "number", "Effort", true)
                },
                rows,
                breakdowns = new[] { "Status", "Priority", "Type", "Assignee" },
                unit = "Issues",
                breakdownTitle = "Issues at a glance"
            };

            var fileName = $"Issues_Report{(project?.Key is { Length: > 0 } key ? "_" + FilePart(key) : "")}_{Day(today)}.xlsx";
            var byStatus = issues.GroupBy(i => Text(i, "statusCode") ?? "UNKNOWN").OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
            string? note = null;
            if (total > read.Count) note = $"Only the first {read.Count} of {total} matching issues were read, so the report is not complete. Ask for a narrower report, for example one project or one status.";

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                count = issues.Count,
                open,
                done,
                overdue,
                storyPoints = points,
                loggedHours = logged,
                byStatus,
                filters = filterText,
                note,
                file = File(fileName, sheet)
            }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static bool IsMe(string said) => said.Trim().ToLowerInvariant() is "me" or "myself" or "i" or "mine" or "my";
}



public class ExportWorkLogReportTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ReportToolBase(factory, accessor)
{
    public override string Name => "export_work_log_report";
    public override string Description =>
        "Prepare an Excel (.xlsx) report of the signed-in user's own time entries for download: date, issue, project, hours, billable, note and the week's timesheet status, with totals and a summary sheet. " +
        "Use it for 'export my timesheet', 'download my work logs for October', 'an Excel of my hours last month' or 'my time report'. Work out fromDate and toDate from what the user said (a month is its first to its last day); with none it covers this month so far. " +
        "It covers up to 500 entries and a year at most. The file is shown as a download below your message: say the totals from the result, never print the file contents, and do not list the entries again.";
    public override string Category => "Chronos";
    public override string RequiredPermission => "WORKLOG_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["fromDate"] = new { type = "STRING", description = "First day, YYYY-MM-DD." },
            ["toDate"] = new { type = "STRING", description = "Last day, YYYY-MM-DD." },
            ["issueIdOrKey"] = new { type = "STRING", description = "Only entries on this issue ('FLP360-17')." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (CallerUserId(arguments) is not { } caller)
                return Fail("I can't tell who you are, so I can't read your time entries. Please sign in again.");

            var today = TodayLocal;
            DateOnly from = default, to = default;
            var gotFrom = Str(arguments, "fromDate");
            var gotTo = Str(arguments, "toDate");
            if (gotFrom != null && !TryDate(gotFrom, out from)) return Fail("fromDate must be written as YYYY-MM-DD.");
            if (gotTo != null && !TryDate(gotTo, out to)) return Fail("toDate must be written as YYYY-MM-DD.");
            if (gotFrom == null && gotTo == null) { from = new DateOnly(today.Year, today.Month, 1); to = today; }
            else if (gotTo == null) to = today < from ? from : today;
            else if (gotFrom == null) from = new DateOnly(to.Year, to.Month, 1);
            if (from > to) return Fail("fromDate is after toDate.");
            if (to.DayNumber - from.DayNumber > 365) return Fail("I can export at most a year of entries at a time.");

            var jira = Jira();
            long? issueId = null;
            if (Str(arguments, "issueIdOrKey") is { } key)
            {
                issueId = await ResolveIssueIdAsync(jira, key, cancellationToken);
                if (issueId == null) return Fail($"Issue '{key}' was not found, or you do not have access to it.");
            }

            string Url(int page) =>
                $"{JiraRoutes.WorkLogs}?From={Day(from)}&To={Day(to)}&UserId={caller}&Page={page}&PageSize={PageSize}" + (issueId.HasValue ? $"&IssueId={issueId}" : "");
            var (read, total, error) = await ReadPagesAsync(jira, Url, "I could not read your time entries", cancellationToken);
            if (error != null) return Fail(error);

            var logs = read
                .Where(l => LongProp(l, "userId") is not { } id || id == caller)
                .OrderBy(l => Text(l, "workDate"), StringComparer.Ordinal)
                .ThenBy(l => Text(l, "startTime"), StringComparer.Ordinal)
                .ThenBy(l => LongProp(l, "id"))
                .ToList();
            if (logs.Count == 0)
                return new ToolResult(true, JsonSerializer.Serialize(new { count = 0, from = Day(from), to = Day(to), note = "Nothing is logged in that range, so no file was prepared." }));

            long Minutes(JsonElement l) => LongProp(l, "durationMinutes") ?? 0;
            bool Billable(JsonElement l) => l.TryGetProperty("billable", out var b) && b.ValueKind == JsonValueKind.True;
            string StatusOf(JsonElement l) => Sentence(Text(l, "periodStatus") ?? Text(l, "status"));

            var rows = logs.Select(l =>
            {
                var date = DateOnly.TryParse(Text(l, "workDate"), CultureInfo.InvariantCulture, out var d) ? d : (DateOnly?)null;
                return new object?[]
                {
                    Text(l, "workDate") ?? "", date?.ToString("ddd", CultureInfo.InvariantCulture) ?? "",
                    Text(l, "issueKey") ?? "", Text(l, "issueSummary") ?? "", Text(l, "projectName") ?? "",
                    Hours(Minutes(l)), Billable(l) ? "Yes" : "No", Text(l, "description") ?? "", StatusOf(l)
                };
            }).ToList();

            var totalMinutes = logs.Sum(Minutes);
            var billableMinutes = logs.Where(Billable).Sum(Minutes);
            var days = logs.Select(l => Text(l, "workDate")).Where(d => d != null).Distinct().Count();
            var rangeText = from == to ? Pretty(from) : $"{Pretty(from)} to {Pretty(to)}";

            var sheet = new
            {
                title = "Time Report",
                subtitle = $"Prepared by Athenna on {Pretty(today)} · {rangeText}" + (issueId.HasValue ? $" · issue {Str(arguments, "issueIdOrKey")!.ToUpperInvariant()}" : ""),
                sheetName = "Time entries",
                summary = new[]
                {
                    new { label = "Total hours", value = Number(Hours(totalMinutes)) },
                    new { label = "Billable hours", value = Number(Hours(billableMinutes)) },
                    new { label = "Days logged", value = days.ToString(CultureInfo.InvariantCulture) },
                    new { label = "Entries", value = logs.Count.ToString(CultureInfo.InvariantCulture) }
                },
                columns = new[]
                {
                    Column("Date", 13, "text", "When"), Column("Day", 8, "tag", "When"),
                    Column("Issue", 14, "code", "Work"), Column("Summary", 44, "text", "Work"), Column("Project", 22, "tag", "Work"),
                    Column("Hours", 10, "number", "Time", true), Column("Billable", 10, "tag", "Time"),
                    Column("Note", 48, "text", "Notes"), Column("Timesheet", 14, "tag", "Notes")
                },
                rows,
                breakdowns = new[] { "Project", "Billable", "Timesheet", "Day" },
                unit = "Entries",
                breakdownTitle = "Time at a glance"
            };

            var fileName = $"Time_Report_{Day(from)}_to_{Day(to)}.xlsx";
            string? note = total > read.Count ? $"Only the first {read.Count} of {total} entries were read, so the totals are too low. Ask for a shorter range." : null;

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                from = Day(from),
                to = Day(to),
                count = logs.Count,
                totalHours = Hours(totalMinutes),
                billableHours = Hours(billableMinutes),
                daysLogged = days,
                note,
                file = File(fileName, sheet)
            }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}

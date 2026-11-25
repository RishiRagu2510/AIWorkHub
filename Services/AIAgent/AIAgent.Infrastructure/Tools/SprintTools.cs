using System.Globalization;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;


internal sealed record SprintProject(long Id, string? Key, string? Name);


internal sealed record SprintRef(SprintProject Project, long BoardId, JsonElement Sprint)
{
    public long Id => Sprint.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
    public string Name => Sprint.TryGetProperty("name", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    public string Status => Sprint.TryGetProperty("status", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}



public class GetSprintOverviewTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    private const int IssuePages = 5;
    private const int IssuePageSize = 100;
    private const int ProjectsScanned = 12;

    public override string Name => "get_sprint_overview";
    public override string Description =>
        "Health check of a Scrum sprint: its dates and days left, issues by status, story points done against planned, overdue and unassigned issues, each person's open load, and plain warning signals. " +
        "Use it for 'how is the sprint going', 'sprint status', 'what is at risk this sprint', 'will we finish the sprint' or 'who has the most open work'. One call is enough. " +
        "Without a project it uses the active sprint; if several projects have one it lists them in activeSprints so the user can pick, and you should ask which. Pass project (name or key) when the user named one, and sprint to look at a sprint other than the active one. " +
        "Read-only: it never starts, completes or changes a sprint. Answer from signals, points, issues and items; never invent figures.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["project"] = new { type = "STRING", description = "Project name or key the user named ('FLP360'). Leave out to use whichever project has an active sprint." },
            ["sprint"] = new { type = "STRING", description = "A sprint name, only when the user asked for a particular sprint instead of the active one." }
        }
    };

    private static ToolResult Fail(string message) => new(false, null, message);

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;

            var jira = Jira();

            
            using var projectDoc = await TryGetAsync(jira, owner.HasValue ? JiraRoutes.ProjectsByUser(owner.Value) : JiraRoutes.Projects, cancellationToken);
            if (projectDoc == null || projectDoc.RootElement.ValueKind != JsonValueKind.Array)
                return Fail("I could not list your projects right now.");
            var projects = projectDoc.RootElement.EnumerateArray()
                .Where(p => LongProp(p, "id").HasValue)
                .Select(p => new SprintProject(LongProp(p, "id")!.Value, Text(p, "key"),
                    new[] { Text(p, "displayName"), Text(p, "projectName"), Text(p, "name") }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))))
                .OrderBy(p => p.Id)
                .ToList();
            if (projects.Count == 0) return Fail("You are not on any project yet, so there is no sprint to show.");

            var named = Str(arguments, "project");
            if (named != null)
            {
                var matches = FindProjects(named, projects);
                if (matches.Count == 0) return Fail($"No project matches '{named}'. Your projects: {string.Join(", ", projects.Take(15).Select(p => p.Key ?? p.Name))}.");
                if (matches.Count > 1) return Fail($"'{named}' fits more than one project: {string.Join(", ", matches.Take(8).Select(p => $"{p.Name} ({p.Key})"))}. Ask the user which one.");
                projects = matches;
            }

            
            var scanned = projects.Take(ProjectsScanned).ToList();
            var perProject = await Task.WhenAll(scanned.Select(p => SprintsOfAsync(jira, p, cancellationToken)));
            var sprints = perProject.SelectMany(s => s).ToList();
            if (sprints.Count == 0)
                return new ToolResult(true, JsonSerializer.Serialize(new
                {
                    overview = false,
                    note = named != null
                        ? $"{scanned[0].Name} has no sprints. Sprints exist only on Scrum boards, so this project may use a Kanban board."
                        : "None of your projects has a sprint. Sprints exist only on Scrum boards."
                }));

            
            SprintRef chosen;
            var asked = Str(arguments, "sprint");
            if (asked != null)
            {
                var byName = sprints.Where(s => s.Name.Contains(asked.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                var exact = byName.Where(s => string.Equals(s.Name, asked.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (exact.Count == 1) byName = exact;
                if (byName.Count == 0) return Fail($"No sprint is called '{asked}'. Sprints: {string.Join(", ", sprints.Take(10).Select(s => $"{s.Name} ({s.Status.ToLowerInvariant()})"))}.");
                if (byName.Count > 1) return Fail($"'{asked}' fits several sprints: {string.Join(", ", byName.Take(8).Select(s => $"{s.Name} in {s.Project.Name}"))}. Ask the user which one.");
                chosen = byName[0];
            }
            else
            {
                var active = sprints.Where(s => s.Status == "ACTIVE").ToList();
                if (active.Count == 0)
                {
                    var recent = sprints.OrderByDescending(s => Text(s.Sprint, "createdAt"), StringComparer.Ordinal).Take(5)
                        .Select(s => new { project = s.Project.Name, sprint = s.Name, status = s.Status, startDate = Text(s.Sprint, "startDate"), endDate = Text(s.Sprint, "endDate") });
                    return new ToolResult(true, JsonSerializer.Serialize(new { overview = false, note = "There is no active sprint right now.", sprints = recent }));
                }
                if (active.Count > 1)
                    return new ToolResult(true, JsonSerializer.Serialize(new
                    {
                        overview = false,
                        needsChoice = true,
                        note = "More than one project has an active sprint. Ask the user which project to look at.",
                        activeSprints = active.Select(s => new { project = s.Project.Name, key = s.Project.Key, sprint = s.Name, endDate = Text(s.Sprint, "endDate") })
                    }));
                chosen = active[0];
            }

            return await OverviewAsync(jira, chosen, owner, cancellationToken);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    internal static List<SprintProject> FindProjects(string said, IReadOnlyList<SprintProject> projects)
    {
        var term = said.Trim();
        var exact = projects.Where(p => string.Equals(p.Key, term, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, term, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0) return exact;
        return projects.Where(p => (p.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) || (p.Key?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
    }

    private async Task<List<SprintRef>> SprintsOfAsync(HttpClient jira, SprintProject project, CancellationToken cancellationToken)
    {
        var found = new List<SprintRef>();
        using var boards = await TryGetAsync(jira, $"{JiraRoutes.Boards}?projectId={project.Id}", cancellationToken);
        if (boards == null || boards.RootElement.ValueKind != JsonValueKind.Array) return found;
        foreach (var board in boards.RootElement.EnumerateArray())
        {
            if (!string.Equals(Text(board, "boardType"), "SCRUM", StringComparison.OrdinalIgnoreCase) || LongProp(board, "id") is not { } boardId) continue;
            using var list = await TryGetAsync(jira, JiraRoutes.BoardSprints(boardId), cancellationToken);
            if (list == null || list.RootElement.ValueKind != JsonValueKind.Array) continue;
            foreach (var sprint in list.RootElement.EnumerateArray()) found.Add(new SprintRef(project, boardId, sprint.Clone()));
        }
        return found;
    }

    private static double Pct(double part, double whole) => whole <= 0 ? 0 : Math.Round(part * 100.0 / whole, 0);

    private async Task<ToolResult> OverviewAsync(HttpClient jira, SprintRef sprint, long? owner, CancellationToken cancellationToken)
    {
        
        var ids = new HashSet<long>();
        for (var page = 1; page <= IssuePages; page++)
        {
            using var backlog = await TryGetAsync(jira, $"{JiraRoutes.BoardBacklog(sprint.BoardId)}?sprintId={sprint.Id}&page={page}", cancellationToken);
            if (backlog == null) return Fail("I could not read the sprint's issues right now.");
            var items = backlog.RootElement.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array ? arr.EnumerateArray().ToList() : [];
            foreach (var item in items) if (LongProp(item, "id") is { } id) ids.Add(id);
            var total = LongProp(backlog.RootElement, "totalCount") ?? items.Count;
            if (items.Count == 0 || ids.Count >= total) break;
        }

        var details = new Dictionary<long, JsonElement>();
        if (ids.Count > 0)
        {
            for (var page = 1; page <= IssuePages && details.Count < ids.Count; page++)
            {
                var url = $"{JiraRoutes.IssueSearch}?ProjectId={sprint.Project.Id}&Page={page}&PageSize={IssuePageSize}&SortDirection=desc"
                    + (owner.HasValue ? $"&AssigneeUserId={owner.Value}" : "");
                using var search = await TryGetAsync(jira, url, cancellationToken);
                if (search == null) return Fail("I could not read the sprint's issue details right now.");
                var items = search.RootElement.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array ? arr.EnumerateArray().ToList() : [];
                foreach (var item in items)
                    if (LongProp(item, "id") is { } id && ids.Contains(id)) details[id] = item.Clone();
                var total = LongProp(search.RootElement, "totalCount") ?? items.Count;
                if (items.Count < IssuePageSize || page * IssuePageSize >= total) break;
            }
        }

        var today = TodayLocal;
        var issues = details.Values.Where(i => owner is not { } o || AssignedTo(i, o)).ToList();
        var cancelled = issues.Count(i => Text(i, "statusCode") == "CANCELLED");
        var counted = issues.Where(i => Text(i, "statusCode") != "CANCELLED").ToList();
        var done = counted.Where(i => Text(i, "statusCode") == "DONE").ToList();
        var open = counted.Where(i => Text(i, "statusCode") != "DONE").ToList();

        double Points(IEnumerable<JsonElement> set) => set.Sum(i => (double)(LongProp(i, "storyPoints") ?? 0));
        var pointsTotal = Points(counted);
        var pointsDone = Points(done);
        var unestimated = counted.Count(i => LongProp(i, "storyPoints") is null or 0);

        
        DateOnly? start = DateOnly.TryParse(Text(sprint.Sprint, "startDate"), CultureInfo.InvariantCulture, out var s) ? s : null;
        DateOnly? end = DateOnly.TryParse(Text(sprint.Sprint, "endDate"), CultureInfo.InvariantCulture, out var e) ? e : null;
        int? daysLeft = end.HasValue ? end.Value.DayNumber - today.DayNumber : null;
        int? daysTotal = start.HasValue && end.HasValue ? end.Value.DayNumber - start.Value.DayNumber + 1 : null;
        double? elapsed = start.HasValue && daysTotal is > 0 ? Math.Clamp(Pct(today.DayNumber - start.Value.DayNumber + 1, daysTotal.Value), 0, 100) : null;

        
        var byStatus = counted.GroupBy(i => Text(i, "statusCode") ?? "UNKNOWN").OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());

        
        DateOnly? Due(JsonElement i) => DateOnly.TryParse(Text(i, "dueDate"), CultureInfo.InvariantCulture, out var d) ? d : null;
        var overdue = open.Where(i => Due(i) is { } d && d < today).OrderBy(i => Due(i)!.Value.DayNumber).ThenBy(i => Text(i, "issueKey"), StringComparer.Ordinal).ToList();
        var unassigned = open.Where(i => LongProp(i, "assigneeUserId") is null && !HasAssignees(i)).OrderBy(i => Text(i, "issueKey"), StringComparer.Ordinal).ToList();
        var notStarted = open.Count(i => Text(i, "statusCode") == "TODO");

        
        IReadOnlyList<DirectoryEntry> people = owner.HasValue ? Array.Empty<DirectoryEntry>() : await PeopleAsync(cancellationToken);
        var load = new Dictionary<long, (int Issues, double Points)>();
        foreach (var issue in open)
            foreach (var who in AssigneesOf(issue))
            {
                var (n, p) = load.GetValueOrDefault(who);
                load[who] = (n + 1, p + (LongProp(issue, "storyPoints") ?? 0));
            }
        var workload = load
            .Select(kv => new { name = owner.HasValue ? "You" : EmployeeDirectory.NameOfUser(people, kv.Key) ?? $"User {kv.Key}", openIssues = kv.Value.Issues, openPoints = kv.Value.Points })
            .OrderByDescending(x => x.openPoints).ThenByDescending(x => x.openIssues).ThenBy(x => x.name, StringComparer.Ordinal)
            .Take(10).ToList();

        string NameOf(JsonElement i) => AssigneesOf(i).FirstOrDefault() is var first && first != 0 ? (owner.HasValue ? "you" : EmployeeDirectory.NameOfUser(people, first) ?? $"user {first}") : "nobody";

        
        var signals = new List<string>();
        var active = sprint.Status == "ACTIVE";
        if (active && daysLeft < 0 && open.Count > 0)
            signals.Add($"The sprint ended {-daysLeft} day{(daysLeft == -1 ? "" : "s")} ago but is still active, with {open.Count} open issue{(open.Count == 1 ? "" : "s")}.");
        if (active && elapsed is { } el && daysLeft >= 0 && counted.Count > 0)
        {
            var donePct = pointsTotal > 0 ? Pct(pointsDone, pointsTotal) : Pct(done.Count, counted.Count);
            if (donePct < el - 20)
                signals.Add($"Behind pace: {donePct}% of the {(pointsTotal > 0 ? "points" : "issues")} are done with {el}% of the time gone.");
        }
        if (overdue.Count > 0) signals.Add($"{overdue.Count} open issue{(overdue.Count == 1 ? " is" : "s are")} past {(overdue.Count == 1 ? "its" : "their")} due date.");
        if (unassigned.Count > 0) signals.Add($"{unassigned.Count} open issue{(unassigned.Count == 1 ? " has" : "s have")} no assignee.");
        if (active && daysLeft is >= 0 and <= 2 && notStarted > 0) signals.Add($"{notStarted} issue{(notStarted == 1 ? " has" : "s have")} not been started with {(daysLeft == 0 ? "the sprint ending today" : $"{daysLeft} day{(daysLeft == 1 ? "" : "s")} left")}.");
        if (unestimated > 0) signals.Add($"{unestimated} issue{(unestimated == 1 ? " has" : "s have")} no story points, so the points figures leave {(unestimated == 1 ? "it" : "them")} out.");

        var attention = new List<object>();
        var seen = new HashSet<long>();
        void Add(JsonElement i, string why)
        {
            if (LongProp(i, "id") is not { } id || !seen.Add(id)) return;
            attention.Add(new
            {
                id,
                issueKey = Text(i, "issueKey"),
                summary = Text(i, "summary"),
                statusCode = Text(i, "statusCode"),
                priority = Text(i, "priority"),
                dueDate = Text(i, "dueDate"),
                assignee = NameOf(i),
                why
            });
        }
        foreach (var i in overdue.Take(5)) Add(i, $"overdue by {today.DayNumber - Due(i)!.Value.DayNumber} day{(today.DayNumber - Due(i)!.Value.DayNumber == 1 ? "" : "s")}");
        foreach (var i in unassigned.Take(5)) Add(i, "nobody is assigned");

        var missing = owner.HasValue ? 0 : ids.Count - details.Count;
        string? note = null;
        if (ids.Count == 0) note = "No issues are in this sprint yet.";
        else if (owner.HasValue) note = "You can see only the issues assigned to you, so these figures are for your part of the sprint, not the whole team.";
        else if (missing > 0) note = $"{missing} issue{(missing == 1 ? "" : "s")} in the sprint could not be read, so the figures may be a little low.";

        return new ToolResult(true, JsonSerializer.Serialize(new
        {
            overview = true,
            project = new { id = sprint.Project.Id, key = sprint.Project.Key, name = sprint.Project.Name },
            sprint = new
            {
                id = sprint.Id,
                name = sprint.Name,
                goal = Text(sprint.Sprint, "goal"),
                status = sprint.Status,
                startDate = start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDate = end?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                daysLeft,
                totalDays = daysTotal,
                timeElapsedPercent = elapsed
            },
            scope = owner.HasValue ? "your issues only" : "the whole sprint",
            issues = new { total = counted.Count, done = done.Count, open = open.Count, cancelled, byStatus },
            points = new { total = pointsTotal, done = pointsDone, remaining = pointsTotal - pointsDone, donePercent = pointsTotal > 0 ? Pct(pointsDone, pointsTotal) : (double?)null, unestimatedIssues = unestimated },
            signals,
            workload,
            items = attention,
            note
        }));
    }

    private static bool HasAssignees(JsonElement issue) =>
        issue.TryGetProperty("assigneeUserIds", out var many) && many.ValueKind == JsonValueKind.Array && many.GetArrayLength() > 0;

    private static IEnumerable<long> AssigneesOf(JsonElement issue)
    {
        var ids = new List<long>();
        if (issue.TryGetProperty("assigneeUserIds", out var many) && many.ValueKind == JsonValueKind.Array)
            ids.AddRange(many.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.Number).Select(a => a.GetInt64()));
        if (issue.TryGetProperty("assigneeUserId", out var one) && one.ValueKind == JsonValueKind.Number && !ids.Contains(one.GetInt64())) ids.Insert(0, one.GetInt64());
        return ids;
    }

    private async Task<IReadOnlyList<DirectoryEntry>> PeopleAsync(CancellationToken cancellationToken) =>
        await EmployeeDirectory.LoadAsync(Identity(), cancellationToken);
}

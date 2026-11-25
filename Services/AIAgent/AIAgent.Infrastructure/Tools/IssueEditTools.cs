using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;


public sealed record LoadedIssue(HttpClient Client, long Id, JsonElement Before, long Version, string Key);

public abstract class IssueEditToolBase(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : BaseAgentTool
{
    protected IHttpClientFactory Factory => httpClientFactory;
    protected IHttpContextAccessor Accessor => httpContextAccessor;

    protected static ToolResult Fail(string message) => new(false, null, message);

    protected async Task<(LoadedIssue? Issue, ToolResult? Failure)> LoadAsync(Dictionary<string, object?> arguments, string idOrKey, CancellationToken cancellationToken)
    {
        var (owner, denied) = IssueScope(arguments);
        if (denied != null) return (null, denied);

        var client = Factory.CreateClient("JiraService");
        AttachBearerToken(client, Accessor);

        var id = await ResolveIssueIdAsync(client, idOrKey, cancellationToken);
        if (id == null) return (null, Fail($"Issue '{idOrKey}' was not found, or you do not have access to it."));

        var response = await client.GetAsync(JiraRoutes.Issue(id), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, Fail(owner.HasValue
                ? $"Issue '{idOrKey}' was not found, or it is not assigned to you."
                : $"Could not read issue '{idOrKey}': {ApiMessage(await response.Content.ReadAsStringAsync(cancellationToken), (int)response.StatusCode)}"));
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var before = doc.RootElement.Clone();

        
        if (owner.HasValue && !AssignedTo(before, owner.Value))
            return (null, Fail($"Issue '{idOrKey}' was not found, or it is not assigned to you."));

        var version = LongProp(before, "version");
        if (version == null)
            return (null, Fail($"Issue '{idOrKey}' did not report a version, so the change was not attempted."));

        return (new LoadedIssue(client, id.Value, before, version.Value, Text(before, "issueKey") ?? idOrKey), null);
    }

    protected static async Task<(bool Ok, string Content, int Status)> PutAsync(LoadedIssue issue, string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{issue.Version}\""));
        using var response = await issue.Client.SendAsync(request, cancellationToken);
        return (response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(cancellationToken), (int)response.StatusCode);
    }

    protected static List<long> AssigneeIds(JsonElement issue)
    {
        var ids = new List<long>();
        if (issue.ValueKind != JsonValueKind.Object) return ids;
        if (issue.TryGetProperty("assigneeUserIds", out var many) && many.ValueKind == JsonValueKind.Array)
            ids.AddRange(many.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.Number).Select(a => a.GetInt64()));
        if (LongProp(issue, "assigneeUserId") is { } single && !ids.Contains(single)) ids.Insert(0, single);
        return ids;
    }

    private protected async Task<IReadOnlyList<DirectoryEntry>> PeopleAsync(CancellationToken cancellationToken)
    {
        var identity = Factory.CreateClient("IdentityTenantService");
        AttachBearerToken(identity, Accessor);
        return await EmployeeDirectory.LoadAsync(identity, cancellationToken);
    }
}


public sealed record AssigneeCandidate(long UserId, string? Name, string? Email);


internal static class PersonMatch
{
    
    public static List<AssigneeCandidate> Find(string term, IReadOnlyList<AssigneeCandidate> people)
    {
        var named = people.Where(m => !string.IsNullOrWhiteSpace(m.Name)).ToList();
        var exact = named.Where(m => string.Equals(m.Name, term, StringComparison.OrdinalIgnoreCase) || string.Equals(m.Email, term, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0) return exact;

        var words = term.Split([' ', '.', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return [];
        return named.Where(m =>
        {
            var parts = m.Name!.Split([' ', '.', '-'], StringSplitOptions.RemoveEmptyEntries);
            return words.All(w => parts.Any(p => p.StartsWith(w, StringComparison.OrdinalIgnoreCase)));
        }).ToList();
    }
}



public class AssignIssueTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : IssueEditToolBase(httpClientFactory, httpContextAccessor)
{
    public override string Name => "assign_issue";
    public override string Description =>
        "Assign an existing issue to a person, add a person to it, or remove its assignee. Accepts an issue key such as 'FLP360-17' or a numeric id. " +
        "Pass assignee as the name the user gave ('Priya', 'Asha Kumar'), 'me' for the signed-in user, or a numeric userId. The tool looks the person up among that issue's project members itself, so do not look the project up first. " +
        "If the name fits nobody or several people it refuses and lists them: show that list and ask the user which one, never pick for them. " +
        "By default the person replaces the current assignee; set addToExisting to true only when the user said to add someone. Set unassign to true only when the user said to remove the assignee. " +
        "Only do this when the user clearly asked for it. Say who it was assigned to before, from previousAssignees.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_ASSIGN";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["issueIdOrKey"] = new { type = "STRING", description = "Issue key ('FLP360-17', 'P2-1') or numeric id." },
            ["assignee"] = new { type = "STRING", description = "Who to assign: a name, 'me', or a numeric userId." },
            ["addToExisting"] = new { type = "BOOLEAN", description = "True only if the user said to add this person and keep the current assignees." },
            ["unassign"] = new { type = "BOOLEAN", description = "True only if the user said to remove the assignee. Then leave assignee out." }
        },
        required = new[] { "issueIdOrKey" }
    };

    
    public static (long? UserId, string? Problem) Resolve(string said, long? caller, IReadOnlyList<AssigneeCandidate> members)
    {
        var term = said.Trim();
        if (term.Length == 0) return (null, "Who should it be assigned to?");

        if (term.Equals("me", StringComparison.OrdinalIgnoreCase) || term.Equals("myself", StringComparison.OrdinalIgnoreCase))
            return caller is > 0 ? (caller, null) : (null, "I can't tell who you are, so I can't assign it to you. Please sign in again.");

        if (long.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return (id, null);

        static string Label(AssigneeCandidate c) => c.Name ?? $"user {c.UserId}";
        var named = members.Where(m => !string.IsNullOrWhiteSpace(m.Name)).ToList();
        if (named.Count == 0)
            return (null, "I could not look up names for this project's members. Call list_project_members and pass the chosen person's userId.");

        var matches = PersonMatch.Find(term, named);

        if (matches.Count == 1) return (matches[0].UserId, null);
        if (matches.Count > 1)
            return (null, $"'{said}' fits more than one person on this project: {string.Join(", ", matches.Take(8).Select(m => $"{Label(m)} (userId {m.UserId})"))}. Ask the user which one, then call assign_issue again with that person's userId.");

        return (null, $"No one on this project is called '{said}'. People who can be assigned: {string.Join(", ", named.OrderBy(m => m.Name).Take(15).Select(m => m.Name))}{(named.Count > 15 ? ", ..." : "")}.");
    }

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var idOrKey = Str(arguments, "issueIdOrKey") ?? "";
            if (idOrKey.Length == 0) return Fail("issueIdOrKey is required.");

            var said = Str(arguments, "assignee") ?? (Long(arguments, "assigneeUserId") is { } n ? n.ToString(CultureInfo.InvariantCulture) : null);
            var unassign = Bool(arguments, "unassign", false);
            var add = Bool(arguments, "addToExisting", false);
            if (unassign && said != null) return Fail("Either name an assignee or set unassign, not both.");
            if (!unassign && said == null) return Fail("Who should it be assigned to? Ask the user for a name.");

            var (loaded, failure) = await LoadAsync(arguments, idOrKey, cancellationToken);
            if (failure != null) return failure;
            var issue = loaded!;

            var previous = AssigneeIds(issue.Before);
            var people = await PeopleAsync(cancellationToken);

            object body;
            List<long> next;
            if (unassign)
            {
                if (previous.Count == 0) return Fail($"{issue.Key} has no assignee to remove.");
                next = [];
                body = new { clearAssignee = true };
            }
            else
            {
                
                var members = new List<AssigneeCandidate>();
                var projectId = LongProp(issue.Before, "projectId");
                var membersKnown = false;
                if (projectId.HasValue)
                {
                    var response = await issue.Client.GetAsync(JiraRoutes.ProjectAssignmentMembers(projectId), cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        using var membersDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                        if (membersDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            membersKnown = true;
                            foreach (var m in membersDoc.RootElement.EnumerateArray())
                            {
                                if (LongProp(m, "userId") is not { } uid) continue;
                                var entry = people.FirstOrDefault(p => p.UserId == uid);
                                members.Add(new AssigneeCandidate(uid, entry?.Name, entry?.Email));
                            }
                        }
                    }
                }

                var (target, problem) = Resolve(said!, CallerUserId(arguments), members);
                if (problem != null) return Fail(problem);
                var targetId = target!.Value;

                if (membersKnown && !members.Any(m => m.UserId == targetId))
                    return Fail($"User {targetId} is not a member of this issue's project, so {issue.Key} was not assigned. Ask the user who else it could go to.");

                if (add && previous.Contains(targetId))
                    return Fail($"They are already one of the assignees of {issue.Key}.");
                if (!add && previous.Contains(targetId))
                    return Fail(previous.Count == 1
                        ? $"{issue.Key} is already assigned to them."
                        : $"They are already one of the assignees of {issue.Key}. Ask the user whether to make them the only assignee, or leave things as they are.");

                next = add ? [.. previous, targetId] : [targetId];

                body = new { assigneeUserId = next[0], assigneeUserIds = next.ToArray() };
            }

            var (ok, content, status) = await PutAsync(issue, JiraRoutes.IssueAssignee(issue.Id), body, cancellationToken);
            if (!ok) return Fail($"{issue.Key} was not reassigned: {ApiMessage(content, status)}");

            JsonElement after = default;
            try { after = JsonDocument.Parse(content).RootElement; } catch (JsonException) { }
            string Names(IEnumerable<long> ids) => string.Join(", ", ids.Select(i => EmployeeDirectory.NameOfUser(people, i) ?? $"user {i}"));

            
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                assigned = true,
                id = issue.Id,
                issueKey = issue.Key,
                summary = Text(after, "summary") ?? Text(issue.Before, "summary"),
                statusCode = Text(after, "statusCode") ?? Text(issue.Before, "statusCode"),
                priority = Text(after, "priority") ?? Text(issue.Before, "priority"),
                dueDate = Text(after, "dueDate") ?? Text(issue.Before, "dueDate"),
                previousAssignees = previous.Count == 0 ? "nobody" : Names(previous),
                assignees = next.Count == 0 ? "nobody" : Names(next),
                version = LongProp(after, "version") ?? issue.Version + 1
            }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}


public class UpdateIssueTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : IssueEditToolBase(httpClientFactory, httpContextAccessor)
{
    private static readonly int[] Fibonacci = [1, 2, 3, 5, 8, 13];
    private static readonly string[] Priorities = ["LOWEST", "LOW", "MEDIUM", "HIGH", "HIGHEST"];

    public override string Name => "update_issue";
    public override string Description =>
        "Change an existing issue: its summary (title), description, priority, due date or story points. Accepts an issue key such as 'FLP360-17' or a numeric id. " +
        "Pass only what the user asked to change. To add to the description without losing the old text use appendToDescription; description replaces it completely. " +
        "To remove the due date or estimate set clearDueDate or clearStoryPoints, only when the user said so. This does not change the assignee (use assign_issue) or the status (use transition_issue). " +
        "Only do this when the user clearly asked for it. The reply lists what changed, from changes.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_UPDATE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["issueIdOrKey"] = new { type = "STRING", description = "Issue key ('FLP360-17', 'P2-1') or numeric id." },
            ["summary"] = new { type = "STRING", description = "The new title." },
            ["description"] = new { type = "STRING", description = "The new description. Replaces the old one completely." },
            ["appendToDescription"] = new { type = "STRING", description = "Text to add after the current description." },
            ["priority"] = new { type = "STRING", description = "LOWEST, LOW, MEDIUM, HIGH or HIGHEST." },
            ["dueDate"] = new { type = "STRING", description = "YYYY-MM-DD, today or later." },
            ["clearDueDate"] = new { type = "BOOLEAN", description = "True only if the user said to remove the due date." },
            ["storyPoints"] = new { type = "INTEGER", description = "Fibonacci estimate: 1, 2, 3, 5, 8 or 13." },
            ["clearStoryPoints"] = new { type = "BOOLEAN", description = "True only if the user said to remove the estimate." }
        },
        required = new[] { "issueIdOrKey" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var idOrKey = Str(arguments, "issueIdOrKey") ?? "";
            if (idOrKey.Length == 0) return Fail("issueIdOrKey is required.");

            var summary = Str(arguments, "summary");
            var description = arguments.TryGetValue("description", out var d) && d != null ? d.ToString() : null;
            var append = Str(arguments, "appendToDescription");
            var priority = Str(arguments, "priority")?.ToUpperInvariant();
            var dueText = Str(arguments, "dueDate");
            var clearDue = Bool(arguments, "clearDueDate", false);
            var points = Int(arguments, "storyPoints");
            var clearPoints = Bool(arguments, "clearStoryPoints", false);

            if (summary is null && description is null && append is null && priority is null && dueText is null && !clearDue && points is null && !clearPoints)
                return Fail("Nothing to change. Ask the user what to change: the title, description, priority, due date or story points.");
            if (description is not null && append is not null) return Fail("Use either description or appendToDescription, not both.");
            if (dueText is not null && clearDue) return Fail("Either give a new due date or clear it, not both.");
            if (points is not null && clearPoints) return Fail("Either give new story points or clear them, not both.");

            
            if (priority == "CRITICAL") priority = "HIGHEST";
            if (priority is not null && !Priorities.Contains(priority)) return Fail($"priority must be one of {string.Join(", ", Priorities)}.");
            if (points is not null && !Fibonacci.Contains(points.Value)) return Fail($"{points} is not a valid estimate. Story points must be 1, 2, 3, 5, 8 or 13. Ask the user to pick one.");
            if (summary is not null && summary.Length > 250) return Fail("The title is too long. Keep it under 250 characters.");

            DateOnly? due = null;
            if (dueText is not null)
            {
                if (!TryDate(dueText, out var parsed)) return Fail("dueDate must be written as YYYY-MM-DD.");
                if (parsed < TodayLocal) return Fail($"{dueText} is in the past. Ask the user for a due date of {TodayLocal:yyyy-MM-dd} or later.");
                due = parsed;
            }

            var (loaded, failure) = await LoadAsync(arguments, idOrKey, cancellationToken);
            if (failure != null) return failure;
            var issue = loaded!;

            var changes = new List<object>();
            var body = new Dictionary<string, object?>();

            void Note(string field, string? from, string? to) => changes.Add(new { field, from, to });

            if (summary is not null && summary != Text(issue.Before, "summary"))
            {
                body["summary"] = summary;
                Note("summary", Text(issue.Before, "summary"), summary);
            }
            if (priority is not null && !string.Equals(priority, Text(issue.Before, "priority"), StringComparison.OrdinalIgnoreCase))
            {
                body["priority"] = priority;
                Note("priority", Text(issue.Before, "priority"), priority);
            }
            if (due.HasValue && due.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != Text(issue.Before, "dueDate"))
            {
                body["dueDate"] = due.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Note("dueDate", Text(issue.Before, "dueDate"), body["dueDate"]!.ToString());
            }
            if (clearDue && Text(issue.Before, "dueDate") is not null)
            {
                body["clearDueDate"] = true;
                Note("dueDate", Text(issue.Before, "dueDate"), null);
            }
            if (points.HasValue && points != LongProp(issue.Before, "storyPoints"))
            {
                body["storyPoints"] = points.Value;
                Note("storyPoints", LongProp(issue.Before, "storyPoints")?.ToString(CultureInfo.InvariantCulture), points.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (clearPoints && LongProp(issue.Before, "storyPoints") is not null)
            {
                body["clearStoryPoints"] = true;
                Note("storyPoints", LongProp(issue.Before, "storyPoints")?.ToString(CultureInfo.InvariantCulture), null);
            }

            var oldDescription = Text(issue.Before, "description") ?? "";
            var newDescription = append is not null
                ? (oldDescription.Length == 0 ? append : $"{oldDescription.TrimEnd()}\n\n{append}")
                : description;
            if (newDescription is not null && newDescription != oldDescription)
            {
                body["description"] = newDescription;
                Note("description", append is not null ? "(kept, text added)" : "(replaced)", append is not null ? append : "(new text)");
            }

            if (body.Count == 0) return Fail($"{issue.Key} already looks like that, so nothing was changed.");

            var (ok, content, status) = await PutAsync(issue, JiraRoutes.Issue(issue.Id), body, cancellationToken);
            if (!ok) return Fail($"{issue.Key} was not updated: {ApiMessage(content, status)}");

            var people = await PeopleAsync(cancellationToken);
            using var after = JsonDocument.Parse(content);
            var shaped = JsonSerializer.SerializeToNode(IssueProjection.Project(after.RootElement, people, TodayLocal))!.AsObject();
            shaped["updated"] = true;
            shaped["changes"] = JsonSerializer.SerializeToNode(changes);
            return new ToolResult(true, shaped.ToJsonString());
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}


public class GetWorkDigestTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : BaseAgentTool
{
    private const int PageSize = 50;

    public override string Name => "get_work_digest";
    public override string Description =>
        "A ready-ranked list of what to work on: issues assigned to the person that are overdue, due within 7 days, already in progress, and everything else open, with counts. " +
        "Use it for 'what should I work on today', 'what is on my plate', 'what is urgent' or 'plan my day': one call is enough, answer from `items` in the order given and explain why each is there from `why`. " +
        "It shows only the most pressing few, so it is NOT for listing issues: 'show my open issues' or 'my bugs' is search_issues. " +
        "By default it is for the signed-in user. Managers and admins may pass userId to look at someone else's work; everyone else sees only their own.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["userId"] = new { type = "INTEGER", description = "Another person's user id. Only managers and admins may ask for this." }
        }
    };

    private static int PriorityRank(string? priority) => priority?.ToUpperInvariant() switch
    {
        "HIGHEST" or "CRITICAL" => 0,
        "HIGH" => 1,
        "MEDIUM" => 2,
        "LOW" => 3,
        "LOWEST" => 4,
        _ => 2
    };

    private sealed record Row(JsonElement Issue, string Group, DateOnly? Due, int Rank, string Key);

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;

            var requested = Long(arguments, "userId");
            var caller = CallerUserId(arguments);
            long? who = requested ?? caller;
            if (owner.HasValue)
            {
                
                if (requested.HasValue && requested != owner) return Fail("You can only see your own work.");
                who = owner;
            }
            if (who is null or <= 0) return Fail("I can't tell whose work to look at. Please sign in again.");

            var client = httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, httpContextAccessor);

            var today = TodayLocal;
            var groups = new[] { "OVERDUE", "DUE_SOON", "IN_PROGRESS", "OTHER" };
            var responses = await Task.WhenAll(groups.Select(async g =>
            {
                var url = $"{JiraRoutes.IssueSearch}?AssigneeUserId={who}&WorkGroup={g}&Today={today:yyyy-MM-dd}&Page=1&PageSize={PageSize}";
                using var response = await client.GetAsync(url, cancellationToken);
                return (Group: g, Ok: response.IsSuccessStatusCode, Status: (int)response.StatusCode, Body: await response.Content.ReadAsStringAsync(cancellationToken));
            }));

            var failed = responses.FirstOrDefault(r => !r.Ok);
            if (failed.Group != null) return Fail($"Could not load the work list: {ApiMessage(failed.Body, failed.Status)}");

            var rows = new List<Row>();
            var totals = new Dictionary<string, long>();
            foreach (var r in responses)
            {
                using var doc = JsonDocument.Parse(r.Body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    totals[r.Group] = 0;
                    continue;
                }
                totals[r.Group] = LongProp(root, "totalCount") ?? items.GetArrayLength();
                foreach (var item in items.EnumerateArray())
                {
                    DateOnly? due = DateOnly.TryParse(Text(item, "dueDate"), CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
                    rows.Add(new Row(item.Clone(), r.Group, due, PriorityRank(Text(item, "priority")), Text(item, "issueKey") ?? ""));
                }
            }

            IReadOnlyList<DirectoryEntry> people = who == caller ? Array.Empty<DirectoryEntry>() : await NamesAsync(cancellationToken);
            var picks = new List<object>();

            
            IEnumerable<Row> Ordered(string group)
            {
                var inGroup = rows.Where(r => r.Group == group);
                return group == "DUE_SOON"
                    ? inGroup.OrderBy(r => r.Due?.DayNumber ?? int.MaxValue).ThenBy(r => r.Rank).ThenBy(r => r.Key, StringComparer.Ordinal)
                    : inGroup.OrderBy(r => r.Rank).ThenBy(r => r.Due?.DayNumber ?? int.MaxValue).ThenBy(r => r.Key, StringComparer.Ordinal);
            }

            void Take(string group, int count)
            {
                foreach (var row in Ordered(group).Take(count)) picks.Add(Shape(row, today));
            }

            Take("OVERDUE", 5);
            Take("DUE_SOON", 5);
            Take("IN_PROGRESS", 4);
            Take("OTHER", 3);

            long Count(string g) => totals.GetValueOrDefault(g);
            var total = totals.Values.Sum();
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                digest = true,
                today = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                forUserId = who,
                forName = people.Count == 0 ? null : EmployeeDirectory.NameOfUser(people, who),
                counts = new { overdue = Count("OVERDUE"), dueWithin7Days = Count("DUE_SOON"), inProgress = Count("IN_PROGRESS"), other = Count("OTHER"), total },
                items = picks,
                note = total == 0
                    ? "Nothing open is assigned to this person."
                    : picks.Count < total ? $"Showing the top {picks.Count} of {total} open issues, most pressing first." : null
            }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static ToolResult Fail(string message) => new(false, null, message);

    private async Task<IReadOnlyList<DirectoryEntry>> NamesAsync(CancellationToken cancellationToken)
    {
        var identity = httpClientFactory.CreateClient("IdentityTenantService");
        AttachBearerToken(identity, httpContextAccessor);
        return await EmployeeDirectory.LoadAsync(identity, cancellationToken);
    }

    private static object Shape(Row row, DateOnly today)
    {
        var issue = row.Issue;
        var priority = Text(issue, "priority");
        var status = Text(issue, "statusCode");
        var days = row.Due.HasValue ? row.Due.Value.DayNumber - today.DayNumber : (int?)null;

        var why = row.Group switch
        {
            "OVERDUE" => $"overdue by {-days} day{(days == -1 ? "" : "s")}, {priority?.ToLowerInvariant()} priority",
            "DUE_SOON" => days == 0 ? $"due today, {priority?.ToLowerInvariant()} priority" : $"due in {days} day{(days == 1 ? "" : "s")}, {priority?.ToLowerInvariant()} priority",
            "IN_PROGRESS" => $"already under way ({status?.ToLowerInvariant().Replace('_', ' ')}), {priority?.ToLowerInvariant()} priority",
            _ => row.Due.HasValue ? $"not started, due {row.Due:yyyy-MM-dd}" : $"not started, no deadline, {priority?.ToLowerInvariant()} priority"
        };

        return new
        {
            id = LongProp(issue, "id"),
            issueKey = Text(issue, "issueKey"),
            summary = Text(issue, "summary"),
            issueType = Text(issue, "issueType"),
            statusCode = status,
            priority,
            dueDate = row.Due?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            storyPoints = LongProp(issue, "storyPoints"),
            remainingHours = (LongProp(issue, "remainingEstimateMinutes") is { } m) ? Math.Round(m / 60.0, 2) : (double?)null,
            group = row.Group.ToLowerInvariant(),
            why
        };
    }
}

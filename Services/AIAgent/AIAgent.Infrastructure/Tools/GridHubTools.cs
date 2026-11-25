using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;

internal static class IssueProjection
{
    public static object Project(JsonElement issue, IReadOnlyList<DirectoryEntry> people, DateOnly today)
    {
        static long? L(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var x) ? x : null;
        static string? S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        static double H(long? minutes) => minutes is null ? 0 : Math.Round(minutes.Value / 60.0, 2);

        object? Person(long? userId) => userId is null ? null : new { userId, name = EmployeeDirectory.NameOfUser(people, userId) };

        var assigneeIds = new List<long>();
        if (issue.TryGetProperty("assigneeUserIds", out var many) && many.ValueKind == JsonValueKind.Array)
            assigneeIds.AddRange(many.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.Number).Select(a => a.GetInt64()));
        if (L(issue, "assigneeUserId") is { } single && !assigneeIds.Contains(single)) assigneeIds.Insert(0, single);

        DateOnly? due = DateOnly.TryParse(S(issue, "dueDate"), CultureInfo.InvariantCulture, out var d) ? d : null;
        var status = S(issue, "statusCode");
        string? dueStatus = null;
        if (due.HasValue)
        {
            var days = due.Value.DayNumber - today.DayNumber;
            dueStatus = status == "DONE" ? "completed"
                : days < 0 ? $"overdue by {-days} day{(days == -1 ? "" : "s")}"
                : days == 0 ? "due today"
                : $"due in {days} day{(days == 1 ? "" : "s")}";
        }

        var description = S(issue, "description");
        if (description is { Length: > 3000 }) description = description[..3000] + "...";

        var labels = issue.TryGetProperty("labels", out var lb) && lb.ValueKind == JsonValueKind.Array
            ? lb.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToArray()
            : [];

        return new
        {
            id = L(issue, "id"),
            issueKey = S(issue, "issueKey"),
            summary = S(issue, "summary"),
            issueType = S(issue, "issueType"),
            statusCode = status,
            priority = S(issue, "priority"),
            projectId = L(issue, "projectId"),
            assignees = assigneeIds.Select(i => Person(i)).ToArray(),
            reporter = Person(L(issue, "reporterUserId")),
            dueDate = due?.ToString("yyyy-MM-dd"),
            dueStatus,
            storyPoints = L(issue, "storyPoints"),
            time = new
            {
                originalEstimateHours = H(L(issue, "originalEstimateMinutes")),
                loggedHours = H(L(issue, "loggedTimeMinutes")),
                remainingHours = H(L(issue, "remainingEstimateMinutes"))
            },
            labels,
            parentIssueId = L(issue, "parentIssueId"),
            description,
            createdAt = S(issue, "createdAt"),
            updatedAt = S(issue, "updatedAt"),
            version = L(issue, "version")
        };
    }
}


public class ListProjectsTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : BaseAgentTool
{
    public override string Name => "list_projects";
    public override string Description => "List the projects the signed-in user can work in, numbered, with their ids and keys. Call this first when the user wants to create an issue and has not said which project, then show the numbered list and let them choose.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "PROJECT_VIEW";
    public override object ParameterSchema => new { type = "OBJECT", properties = new Dictionary<string, object>() };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;

            var client = httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, httpContextAccessor);

            
            var url = owner.HasValue ? JiraRoutes.ProjectsByUser(owner.Value) : JiraRoutes.Projects;
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"Could not list projects: {ApiMessage(await response.Content.ReadAsStringAsync(cancellationToken), (int)response.StatusCode)}");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new ToolResult(false, null, "The project service returned an unexpected reply.");

            var projects = doc.RootElement.EnumerateArray()
                .Select(p => new
                {
                    id = LongProp(p, "id"),
                    key = Text(p, "key"),
                    name = new[] { Text(p, "displayName"), Text(p, "projectName"), Text(p, "name") }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                    status = Text(p, "status")
                })
                .Where(p => p.id.HasValue)
                .OrderBy(p => p.id)
                .Select((p, i) => new { number = i + 1, projectId = p.id, p.key, p.name, p.status })
                .ToList();

            return new ToolResult(true, JsonSerializer.Serialize(new { count = projects.Count, projects }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class ListProjectMembersTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : BaseAgentTool
{
    public override string Name => "list_project_members";
    public override string Description => "List the people who can be assigned issues in one project, with names and user ids. Use it after the project is chosen, to let the user pick an assignee. Only people on that project are returned.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_ASSIGN";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["projectId"] = new { type = "INTEGER", description = "The projectId from list_projects." }
        },
        required = new[] { "projectId" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectId = Long(arguments, "projectId");
            if (projectId is null or <= 0) return new ToolResult(false, null, "projectId is required. Call list_projects and ask the user which project.");

            var client = httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, httpContextAccessor);

            var response = await client.GetAsync(JiraRoutes.ProjectAssignmentMembers(projectId), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"Could not list project members: {ApiMessage(await response.Content.ReadAsStringAsync(cancellationToken), (int)response.StatusCode)}");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var userIds = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().Select(m => LongProp(m, "userId")).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList()
                : [];

            var identity = httpClientFactory.CreateClient("IdentityTenantService");
            AttachBearerToken(identity, httpContextAccessor);
            var people = await EmployeeDirectory.LoadAsync(identity, cancellationToken);

            var members = userIds
                .Select(id => new { userId = id, name = EmployeeDirectory.NameOfUser(people, id), employeeCode = people.FirstOrDefault(p => p.UserId == id)?.Code })
                .OrderBy(m => m.name ?? "~")
                .Select((m, i) => new { number = i + 1, m.userId, m.name, m.employeeCode })
                .ToList();

            return new ToolResult(true, JsonSerializer.Serialize(new { projectId, count = members.Count, members }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class CreateIssueTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : BaseAgentTool
{
    private static readonly int[] Fibonacci = [1, 2, 3, 5, 8, 13];
    private static readonly string[] IssueTypes = ["TASK", "BUG", "STORY", "EPIC"];
    private static readonly string[] Priorities = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];

    
    public const string OnboardingChecklist = """
        ## Onboarding checklist

        ### Before day one
        - [ ] Confirm the offer acceptance and joining date with HR
        - [ ] Create the work email and add the person to the AIWorkHub workspace
        - [ ] Order the laptop, accessories and access card

        ### Day one
        - [ ] Welcome meeting and team introductions
        - [ ] Hand over equipment and verify every login works
        - [ ] Share the code of conduct, leave and timesheet policies

        ### First week
        - [ ] Assign an onboarding buddy
        - [ ] Grant repository, project and tool access
        - [ ] Walk through the codebase and development setup
        - [ ] Agree first-sprint goals with the manager

        ### First 30 days
        - [ ] Complete mandatory training
        - [ ] 30-day check-in with the manager and HR
        """;

    public override string Name => "create_issue";
    public override string Description =>
        "Create a GridHub issue. Never guess: if anything required is missing the tool refuses and lists what to ask. " +
        "Collect, in this order and one question at a time: project (list_projects), assignee (list_project_members), story points (1, 2, 3, 5, 8 or 13), due date (YYYY-MM-DD). " +
        "Set unassigned / noStoryPoints / noDueDate to true only when the user explicitly says to skip that item. " +
        "For a new-employee onboarding ticket set template to EMPLOYEE_ONBOARDING, which adds the standard checklist.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_CREATE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["projectId"] = new { type = "INTEGER", description = "The projectId the user picked from list_projects." },
            ["summary"] = new { type = "STRING", description = "Short title of the issue." },
            ["description"] = new { type = "STRING", description = "Context or steps to reproduce." },
            ["issueType"] = new { type = "STRING", description = "TASK (default), BUG, STORY or EPIC." },
            ["priority"] = new { type = "STRING", description = "LOW, MEDIUM (default), HIGH or CRITICAL." },
            ["assigneeUserId"] = new { type = "INTEGER", description = "userId of a member of that project, from list_project_members." },
            ["storyPoints"] = new { type = "INTEGER", description = "Fibonacci estimate: 1, 2, 3, 5, 8 or 13." },
            ["dueDate"] = new { type = "STRING", description = "YYYY-MM-DD, today or later." },
            ["unassigned"] = new { type = "BOOLEAN", description = "True only if the user said to leave it unassigned." },
            ["noStoryPoints"] = new { type = "BOOLEAN", description = "True only if the user said to skip the estimate." },
            ["noDueDate"] = new { type = "BOOLEAN", description = "True only if the user said there is no due date." },
            ["template"] = new { type = "STRING", description = "EMPLOYEE_ONBOARDING for a new-employee ticket with the standard checklist." }
        },
        required = new[] { "projectId", "summary" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = Str(arguments, "template")?.ToUpperInvariant();
            if (template != null && template != "EMPLOYEE_ONBOARDING")
                return new ToolResult(false, null, "template must be EMPLOYEE_ONBOARDING, or left out.");
            var onboarding = template == "EMPLOYEE_ONBOARDING";

            var projectId = Long(arguments, "projectId");
            var summary = Str(arguments, "summary");
            var assignee = Long(arguments, "assigneeUserId");
            var storyPoints = Int(arguments, "storyPoints");
            var dueText = Str(arguments, "dueDate");

            var missing = new List<string>();
            if (projectId is null or <= 0) missing.Add("the project (call list_projects and let the user choose)");
            if (summary == null) missing.Add("a summary");
            if (!onboarding)
            {
                if (assignee == null && !Bool(arguments, "unassigned", false)) missing.Add("the assignee (call list_project_members and let the user choose)");
                if (storyPoints == null && !Bool(arguments, "noStoryPoints", false)) missing.Add("story points (1, 2, 3, 5, 8 or 13)");
                if (dueText == null && !Bool(arguments, "noDueDate", false)) missing.Add("a due date (YYYY-MM-DD)");
            }
            if (missing.Count > 0)
                return new ToolResult(false, null, $"The issue was not created. Still needed from the user: {string.Join("; ", missing)}. Ask one question at a time and do not guess.");

            var issueType = (Str(arguments, "issueType") ?? "TASK").ToUpperInvariant();
            if (onboarding) issueType = "TASK";
            if (!IssueTypes.Contains(issueType)) return new ToolResult(false, null, $"issueType must be one of {string.Join(", ", IssueTypes)}.");
            var priority = (Str(arguments, "priority") ?? "MEDIUM").ToUpperInvariant();
            if (!Priorities.Contains(priority)) return new ToolResult(false, null, $"priority must be one of {string.Join(", ", Priorities)}.");
            if (storyPoints.HasValue && !Fibonacci.Contains(storyPoints.Value))
                return new ToolResult(false, null, $"{storyPoints} is not a valid estimate. Story points must be 1, 2, 3, 5, 8 or 13. Ask the user to pick one.");

            DateOnly? due = null;
            if (dueText != null)
            {
                if (!TryDate(dueText, out var parsedDue)) return new ToolResult(false, null, "dueDate must be written as YYYY-MM-DD.");
                if (parsedDue < TodayLocal) return new ToolResult(false, null, $"{dueText} is in the past. Ask the user for a due date of {TodayLocal:yyyy-MM-dd} or later.");
                due = parsedDue;
            }

            var client = httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, httpContextAccessor);

            
            if (assignee.HasValue)
            {
                var membersResponse = await client.GetAsync(JiraRoutes.ProjectAssignmentMembers(projectId), cancellationToken);
                if (membersResponse.IsSuccessStatusCode)
                {
                    using var membersDoc = JsonDocument.Parse(await membersResponse.Content.ReadAsStringAsync(cancellationToken));
                    if (membersDoc.RootElement.ValueKind == JsonValueKind.Array
                        && !membersDoc.RootElement.EnumerateArray().Any(m => LongProp(m, "userId") == assignee))
                    {
                        return new ToolResult(false, null, $"User {assignee} is not a member of project {projectId}, so the issue was not created. Call list_project_members and let the user choose again.");
                    }
                }
            }

            var description = Str(arguments, "description") ?? "";
            if (onboarding) description = string.IsNullOrWhiteSpace(description) ? OnboardingChecklist : $"{description}\n\n{OnboardingChecklist}";

            var payload = new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["summary"] = summary,
                ["description"] = description,
                ["issueType"] = issueType,
                ["priority"] = priority,
                ["statusCode"] = "TODO"
            };
            if (storyPoints.HasValue) payload["storyPoints"] = storyPoints.Value;
            if (due.HasValue) payload["dueDate"] = due.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (assignee.HasValue)
            {
                payload["assigneeUserId"] = assignee.Value;
                payload["assigneeUserIds"] = new[] { assignee.Value };
            }
            if (onboarding) payload["labels"] = new[] { "onboarding" };

            var response = await client.PostAsJsonAsync(JiraRoutes.Issues, payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"The issue was not created: {ApiMessage(content, (int)response.StatusCode)}");

            var identity = httpClientFactory.CreateClient("IdentityTenantService");
            AttachBearerToken(identity, httpContextAccessor);
            var people = assignee.HasValue ? await EmployeeDirectory.LoadAsync(identity, cancellationToken) : [];

            
            using var created = JsonDocument.Parse(content);
            var shaped = JsonSerializer.SerializeToNode(IssueProjection.Project(created.RootElement, people, TodayLocal))!.AsObject();
            shaped["created"] = true;
            return new ToolResult(true, shaped.ToJsonString());
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}



public class PrepareIssueTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : BaseAgentTool
{
    private static readonly string[] IssueTypes = ["TASK", "BUG", "STORY", "EPIC"];
    private static readonly string[] Priorities = ["LOWEST", "LOW", "MEDIUM", "HIGH", "HIGHEST"];

    public override string Name => "prepare_issue";
    public override string Description =>
        "Show the user a New issue form in the chat. They pick the project first, then who to assign it to, then story points and a due date, and press Create. " +
        "ALWAYS use this to create an issue; it is the only way to create one. Pass only what the user actually said: summary (a short title from their request), and description, issueType or priority if they stated them. " +
        "If the user named a project, pass it as project: it is highlighted but they still click it. Never choose the project, assignee, story points or due date yourself. For a new-employee onboarding ticket set template to EMPLOYEE_ONBOARDING. " +
        "The form is the answer, so reply with one short sentence.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_CREATE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["summary"] = new { type = "STRING", description = "Short title taken from the user's request." },
            ["description"] = new { type = "STRING", description = "Only if the user gave more detail." },
            ["issueType"] = new { type = "STRING", description = "TASK, BUG, STORY or EPIC, only if the user said." },
            ["priority"] = new { type = "STRING", description = "LOWEST, LOW, MEDIUM, HIGH or HIGHEST, only if the user said." },
            ["project"] = new { type = "STRING", description = "A project the user named (key or name), only if they said one. It is highlighted as a suggestion; the user still has to click it." },
            ["template"] = new { type = "STRING", description = "EMPLOYEE_ONBOARDING for a new-employee ticket with the standard checklist." }
        }
    };

    
    public static long? MatchProject(IEnumerable<(long Id, string? Key, string? Name)> projects, string? said)
    {
        var term = (said ?? "").Trim();
        if (term.Length == 0) return null;
        var list = projects.ToList();
        bool Is(string? value) => string.Equals(value?.Trim(), term, StringComparison.OrdinalIgnoreCase);
        var exact = list.Where(p => Is(p.Key) || Is(p.Name)).ToList();
        if (exact.Count == 1) return exact[0].Id;
        if (exact.Count > 1) return null;
        var partial = list.Where(p => (p.Key?.StartsWith(term, StringComparison.OrdinalIgnoreCase) ?? false)
                                      || (p.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        return partial.Count == 1 ? partial[0].Id : null;
    }

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;

            var template = Str(arguments, "template")?.ToUpperInvariant();
            if (template != null && template != "EMPLOYEE_ONBOARDING")
                return new ToolResult(false, null, "template must be EMPLOYEE_ONBOARDING, or left out.");
            var onboarding = template == "EMPLOYEE_ONBOARDING";

            var client = httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, httpContextAccessor);

            
            var url = owner.HasValue ? JiraRoutes.ProjectsByUser(owner.Value) : JiraRoutes.Projects;
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"Could not load your projects: {ApiMessage(await response.Content.ReadAsStringAsync(cancellationToken), (int)response.StatusCode)}");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new ToolResult(false, null, "The project service returned an unexpected reply.");

            var projects = doc.RootElement.EnumerateArray()
                .Select(p => new
                {
                    id = LongProp(p, "id"),
                    key = Text(p, "key"),
                    name = new[] { Text(p, "displayName"), Text(p, "projectName"), Text(p, "name") }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                    status = Text(p, "status")
                })
                .Where(p => p.id.HasValue && !string.Equals(p.status, "ARCHIVED", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.key ?? p.name)
                .Select(p => new { p.id, p.key, p.name })
                .ToList();

            
            var suggested = MatchProject(projects.Select(p => (p.id!.Value, p.key, p.name)), Str(arguments, "project"));

            var type = (Str(arguments, "issueType") ?? "TASK").ToUpperInvariant();
            if (!IssueTypes.Contains(type) || onboarding) type = "TASK";
            var priority = (Str(arguments, "priority") ?? "MEDIUM").ToUpperInvariant();
            
            if (priority == "CRITICAL") priority = "HIGHEST";
            if (!Priorities.Contains(priority)) priority = "MEDIUM";

            var description = Str(arguments, "description") ?? "";
            if (onboarding) description = string.IsNullOrWhiteSpace(description) ? CreateIssueTool.OnboardingChecklist : $"{description}\n\n{CreateIssueTool.OnboardingChecklist}";

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                issueForm = true,
                today = TodayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                summary = Str(arguments, "summary") ?? "",
                description,
                issueType = type,
                priority,
                labels = onboarding ? new[] { "onboarding" } : Array.Empty<string>(),
                projects,
                suggestedProjectId = suggested,
                note = projects.Count == 0 ? "You are not a member of any project yet, so there is nowhere to create an issue." : null
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}



public class AddIssueCommentTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : BaseAgentTool
{
    private const int MaxBody = 20000;

    public override string Name => "add_issue_comment";
    public override string Description =>
        "Post a comment on an issue. Accepts a key such as 'FLP360-17' or a numeric id. The reporter and the assignees are notified of every comment. " +
        "Set notifyReporter to true when the message is meant for the reporter (for example 'ask the reporter for the steps to reproduce'): it mentions them, so they get a direct notification. " +
        "Use the user's own words; if they only described what to say, write one or two plain sentences. After posting, tell the user exactly what was sent and who was notified. Never post anything the user did not ask for.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_COMMENT_CREATE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["issueIdOrKey"] = new { type = "STRING", description = "Issue key ('FLP360-17') or numeric id." },
            ["comment"] = new { type = "STRING", description = "The comment text to post." },
            ["notifyReporter"] = new { type = "BOOLEAN", description = "True when the comment is addressed to the issue's reporter, so they are mentioned." }
        },
        required = new[] { "issueIdOrKey", "comment" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var idOrKey = Str(arguments, "issueIdOrKey") ?? "";
            var text = Str(arguments, "comment") ?? "";
            if (idOrKey.Length == 0) return new ToolResult(false, null, "issueIdOrKey is required. Ask the user which issue.");
            if (text.Length == 0) return new ToolResult(false, null, "comment is required. Ask the user what to say.");
            if (text.Length > MaxBody) return new ToolResult(false, null, $"The comment is too long ({text.Length} characters). The limit is {MaxBody}.");

            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;

            var client = httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, httpContextAccessor);

            var notFound = new ToolResult(false, null, $"Issue '{idOrKey}' was not found, or you do not have access to it.");
            var issueId = await ResolveIssueIdAsync(client, idOrKey, cancellationToken);
            if (issueId == null) return notFound;

            var issueResponse = await client.GetAsync(JiraRoutes.Issue(issueId.Value), cancellationToken);
            if (!issueResponse.IsSuccessStatusCode) return notFound;
            using var issueDoc = JsonDocument.Parse(await issueResponse.Content.ReadAsStringAsync(cancellationToken));
            var issue = issueDoc.RootElement;
            if (owner.HasValue && !AssignedTo(issue, owner.Value)) return notFound;

            var issueKey = Text(issue, "issueKey") ?? idOrKey;
            var reporterId = LongProp(issue, "reporterUserId");
            var caller = CallerUserId(arguments);

            var mention = new List<long>();
            string? note = null;
            if (Bool(arguments, "notifyReporter", false))
            {
                if (reporterId is null) note = $"{issueKey} has no reporter on record, so nobody was mentioned.";
                else if (reporterId == caller) note = "You are the reporter of this issue, so there was nobody else to mention.";
                else mention.Add(reporterId.Value);
            }

            var response = await client.PostAsJsonAsync(JiraRoutes.IssueComments(issueId.Value), new { body = text, mentionedUserIds = mention }, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"The comment was not posted on {issueKey}: {ApiMessage(content, (int)response.StatusCode)}");

            long? commentId = null;
            try { commentId = LongProp(JsonDocument.Parse(content).RootElement, "id"); } catch (JsonException) { }

            string? reporterName = null;
            if (reporterId.HasValue)
            {
                try
                {
                    var identity = httpClientFactory.CreateClient("IdentityTenantService");
                    AttachBearerToken(identity, httpContextAccessor);
                    reporterName = EmployeeDirectory.NameOfUser(await EmployeeDirectory.LoadAsync(identity, cancellationToken), reporterId.Value);
                }
                catch (Exception)
                {
                    
                }
            }

            
            
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                commented = true,
                issue = issueKey,
                issueTitle = Text(issue, "summary"),
                commentId,
                comment = text,
                reporterUserId = reporterId,
                reporterName,
                reporterMentioned = mention.Count > 0,
                note
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}

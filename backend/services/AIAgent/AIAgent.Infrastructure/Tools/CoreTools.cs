using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;

public abstract class BaseAgentTool : IAgentTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public virtual string? RequiredPortal => null;
    public abstract string RequiredPermission { get; }
    public abstract object ParameterSchema { get; }

    public abstract Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default);

    protected void AttachBearerToken(HttpClient client, IHttpContextAccessor httpContextAccessor)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authHeader.Substring(7).Trim());
            }
            else
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authHeader.Trim());
            }
        }
    }
    
    protected static DateOnly TodayLocal => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    protected static string? Str(Dictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out var v) && v != null && !string.IsNullOrWhiteSpace(v.ToString()) ? v.ToString()!.Trim() : null;

    protected static long? Long(Dictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out var v) && v != null && long.TryParse(v.ToString(), out var n) ? n : null;

    protected static bool Bool(Dictionary<string, object?> args, string key, bool fallback)
    {
        if (!args.TryGetValue(key, out var v) || v == null) return fallback;
        return v switch
        {
            bool b => b,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            _ => bool.TryParse(v.ToString(), out var parsed) ? parsed : fallback
        };
    }

    protected static bool TryDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    protected static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    protected static int? Int(Dictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out var v) && v != null && int.TryParse(v.ToString(), out var n) ? n : null;

    
    protected static long? CallerUserId(Dictionary<string, object?> args) => Long(args, IssueVisibility.CallerUserIdArgument);

    
    protected static bool InAnyRole(Dictionary<string, object?> args, params string[] roles)
    {
        var held = (Str(args, IssueVisibility.CallerRolesArgument) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        
        var wanted = roles.Concat(roles.Any(r => r is "HR" or "HR_ADMIN") ? ["HUMAN_RESOURCE"] : []).ToArray();
        return held.Any(h => wanted.Contains(h, StringComparer.OrdinalIgnoreCase));
    }

    protected static double Hours(long minutes) => Math.Round(minutes / 60.0, 2);

    
    protected static string ApiMessage(string body, int status)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "message", "detail", "error", "title" })
                    {
                        if (doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                            return v.GetString()!;
                    }
                }
            }
            catch (JsonException)
            {
                if (body.Length <= 300) return body.Trim();
            }
        }

        return status switch
        {
            401 => "The session was not accepted. Please sign in again.",
            403 => "You do not have permission to do that.",
            404 => "That record was not found, or you cannot access it.",
            409 => "The record changed in the meantime. Please try again.",
            _ => $"The service answered HTTP {status}."
        };
    }

    protected static long? LongProp(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;

    protected static double? NumProp(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    
    protected static string Cap(string text, int max = 20000) => text.Length <= max ? text : text[..max] + "...(truncated)";

    




    protected static (long? Owner, ToolResult? Denied) IssueScope(Dictionary<string, object?> arguments)
    {
        if (Bool(arguments, IssueVisibility.UnknownCallerArgument, false))
            return (null, new ToolResult(false, null, "I can't tell who you are, so I can't show issues. Please sign in again."));
        return (Long(arguments, IssueVisibility.OwnerArgument), null);
    }

    protected static bool AssignedTo(JsonElement issue, long userId)
    {
        if (issue.ValueKind != JsonValueKind.Object) return false;
        if (issue.TryGetProperty("assigneeUserId", out var single) && single.ValueKind == JsonValueKind.Number && single.GetInt64() == userId)
            return true;
        if (issue.TryGetProperty("assigneeUserIds", out var many) && many.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in many.EnumerateArray())
                if (id.ValueKind == JsonValueKind.Number && id.GetInt64() == userId) return true;
        }
        return false;
    }

    
    protected static string KeepAssignedTo(string searchJson, long userId)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(searchJson);
        if (root?["items"] is not System.Text.Json.Nodes.JsonArray items) return searchJson;

        var removed = 0;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            using var doc = JsonDocument.Parse(items[i]!.ToJsonString());
            if (AssignedTo(doc.RootElement, userId)) continue;
            items.RemoveAt(i);
            removed++;
        }

        if (removed > 0 && root["totalCount"] is { } total)
            root["totalCount"] = Math.Max(items.Count, total.GetValue<int>() - removed);
        return root.ToJsonString();
    }

    protected static async Task<long?> ResolveIssueIdAsync(HttpClient client, string idOrKey, CancellationToken cancellationToken)
    {
        idOrKey = idOrKey.Trim();
        if (long.TryParse(idOrKey, out var id)) return id > 0 ? id : null;

        
        foreach (var candidate in new[] { idOrKey, idOrKey.ToUpperInvariant() }.Distinct())
        {
            var byKey = await client.GetAsync(JiraRoutes.IssueByKey(Uri.EscapeDataString(candidate)), cancellationToken);
            if (!byKey.IsSuccessStatusCode) continue;
            using var doc = JsonDocument.Parse(await byKey.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.TryGetProperty("id", out var idElem)) return idElem.GetInt64();
        }

        
        
        var match = System.Text.RegularExpressions.Regex.Match(idOrKey, @"^(.*?)-(\d+)$");
        if (!match.Success) return null;
        var prefix = match.Groups[1].Value;
        var number = match.Groups[2].Value;

        var search = await client.GetAsync($"{JiraRoutes.IssueSearch}?Page=1&PageSize=100&Text={Uri.EscapeDataString(idOrKey)}", cancellationToken);
        if (search.IsSuccessStatusCode)
        {
            using var searchDoc = JsonDocument.Parse(await search.Content.ReadAsStringAsync(cancellationToken));
            if (searchDoc.RootElement.ValueKind == JsonValueKind.Object
                && searchDoc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var key = Text(item, "issueKey") ?? "";
                    if (key.Equals(idOrKey, StringComparison.OrdinalIgnoreCase)
                        || (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && key.EndsWith("-" + number, StringComparison.Ordinal)))
                    {
                        return item.TryGetProperty("id", out var idElem) ? idElem.GetInt64() : null;
                    }
                }
            }
        }

        return await ResolveByProjectPrefixAsync(client, prefix, number, cancellationToken);
    }

    



    private static async Task<long?> ResolveByProjectPrefixAsync(HttpClient client, string prefix, string number, CancellationToken ct)
    {
        if (!long.TryParse(number, out var issueNumber)) return null;
        static string Norm(string? text) => System.Text.RegularExpressions.Regex.Replace((text ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
        var wanted = Norm(prefix);
        if (wanted.Length == 0) return null;

        var projectsResponse = await client.GetAsync(JiraRoutes.Projects, ct);
        if (!projectsResponse.IsSuccessStatusCode) return null;
        using var projectsDoc = JsonDocument.Parse(await projectsResponse.Content.ReadAsStringAsync(ct));
        if (projectsDoc.RootElement.ValueKind != JsonValueKind.Array) return null;

        var candidates = projectsDoc.RootElement.EnumerateArray()
            .Select(p => (Id: p.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt64() : 0,
                          Key: Norm(Text(p, "key")),
                          Name: Norm(Text(p, "displayName") is { Length: > 0 } dn ? dn : Text(p, "name"))))
            .Where(p => p.Id > 0 && (p.Key == wanted || p.Key.StartsWith(wanted) || p.Name.StartsWith(wanted)))
            .OrderBy(p => p.Key == wanted ? 0 : 1)
            .Take(4)
            .ToList();

        var found = new HashSet<long>();
        foreach (var project in candidates)
        {
            var response = await client.GetAsync($"{JiraRoutes.IssueSearch}?ProjectId={project.Id}&Page=1&PageSize=100", ct);
            if (!response.IsSuccessStatusCode) continue;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in items.EnumerateArray())
            {
                if (LongProp(item, "issueNumber") == issueNumber && LongProp(item, "id") is { } id) found.Add(id);
            }
        }

        return found.Count == 1 ? found.First() : null;
    }
}


public class SearchIssuesTool : BaseAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SearchIssuesTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public override string Name => "search_issues";
    public override string Description => "Search issues in GridHub by query text, status, or priority (e.g. HIGH, CRITICAL).";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "Keyword or issue text to search for (use empty or '*' for all)" },
            ["priority"] = new { type = "STRING", description = "Filter by priority: 'HIGH', 'CRITICAL', 'MEDIUM', 'LOW'" },
            ["statusCode"] = new { type = "STRING", description = "Filter by status: 'TODO', 'IN_PROGRESS', 'TESTING', 'DONE'" },
            ["assigneeUserId"] = new { type = "INTEGER", description = "Filter by assignee user ID" },
            ["projectId"] = new { type = "INTEGER", description = "Only issues in this project ID" },
            ["dueFrom"] = new { type = "STRING", description = "Only issues due on or after this date, YYYY-MM-DD" },
            ["dueTo"] = new { type = "STRING", description = "Only issues due on or before this date, YYYY-MM-DD" }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, _httpContextAccessor);

            var query = arguments.TryGetValue("query", out var q) ? q?.ToString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(query) && arguments.TryGetValue("text", out var t)) query = t?.ToString() ?? "";

            var priority = arguments.TryGetValue("priority", out var p) ? p?.ToString() ?? "" : "";
            var statusCode = arguments.TryGetValue("statusCode", out var sc) ? sc?.ToString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(statusCode) && arguments.TryGetValue("status", out var st)) statusCode = st?.ToString() ?? "";

            long? assigneeUserId = null;
            if (arguments.TryGetValue("assigneeUserId", out var au) && au != null)
            {
                if (au is JsonElement elem && elem.ValueKind == JsonValueKind.Number)
                    assigneeUserId = elem.GetInt64();
                else if (long.TryParse(au.ToString(), out var parsedAu))
                    assigneeUserId = parsedAu;
            }

            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;
            if (owner.HasValue)
            {
                
                if (assigneeUserId.HasValue && assigneeUserId.Value != owner.Value)
                    return new ToolResult(false, null, "You can only view issues assigned to you.");
                assigneeUserId = owner;
            }

            var url = $"{JiraRoutes.IssueSearch}?Page=1&PageSize=20";
            if (!string.IsNullOrWhiteSpace(query) && query != "*") url += $"&Text={Uri.EscapeDataString(query)}";
            if (!string.IsNullOrWhiteSpace(priority)) url += $"&Priority={Uri.EscapeDataString(priority)}";
            if (!string.IsNullOrWhiteSpace(statusCode)) url += $"&Status={Uri.EscapeDataString(statusCode)}";
            if (assigneeUserId.HasValue) url += $"&AssigneeUserId={assigneeUserId.Value}";
            if (Long(arguments, "projectId") is { } searchProjectId) url += $"&ProjectId={searchProjectId}";
            if (TryDate(Str(arguments, "dueFrom"), out var dueFrom)) url += $"&DueFrom={dueFrom:yyyy-MM-dd}";
            if (TryDate(Str(arguments, "dueTo"), out var dueTo)) url += $"&DueTo={dueTo:yyyy-MM-dd}";
            
            var response = await client.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (owner.HasValue) content = KeepAssignedTo(content, owner.Value);
                return new ToolResult(true, content);
            }

            return new ToolResult(false, null, $"Failed to search issues: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class GetIssueDetailsTool : BaseAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetIssueDetailsTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public override string Name => "get_issue_details";
    public override string Description => "Get one issue as structured telemetry: status, priority, type, assignees, reporter, due date and how close it is, story points, original / logged / remaining hours, labels, parent and description. Accepts a key such as 'FLP360-17' or 'P2-1', a project-prefixed shorthand, or a numeric id. Use the result to explain the issue and propose concrete next steps.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["issueKey"] = new { type = "STRING", description = "Issue key ('FLP360-17', 'P2-1'), the key in any letter case, or the numeric id ('17')." }
        },
        required = new[] { "issueKey" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = Str(arguments, "issueKey") ?? Str(arguments, "issueIdOrKey") ?? "";
            if (key.Length == 0) return new ToolResult(false, null, "issueKey is required.");

            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;

            var client = _httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, _httpContextAccessor);

            
            var notFound = new ToolResult(false, null, owner.HasValue
                ? $"Issue '{key}' was not found, or it is not assigned to you."
                : $"Issue '{key}' was not found, or you do not have access to it.");

            var json = await FetchAsync(client, key, cancellationToken);
            if (json == null) return notFound;

            using var doc = JsonDocument.Parse(json);
            if (owner.HasValue && !AssignedTo(doc.RootElement, owner.Value)) return notFound;

            var identity = _httpClientFactory.CreateClient("IdentityTenantService");
            AttachBearerToken(identity, _httpContextAccessor);
            var people = await EmployeeDirectory.LoadAsync(identity, cancellationToken);

            return new ToolResult(true, JsonSerializer.Serialize(IssueProjection.Project(doc.RootElement, people, TodayLocal)));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }

    private static async Task<string?> FetchAsync(HttpClient client, string key, CancellationToken ct)
    {
        async Task<string?> ById(long id)
        {
            var response = await client.GetAsync(JiraRoutes.Issue(id), ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
        }

        if (long.TryParse(key, out var number))
        {
            
            if (await ById(number) is { } byId) return byId;

            var search = await client.GetAsync($"{JiraRoutes.IssueSearch}?Page=1&PageSize=50", ct);
            if (!search.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await search.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;
            foreach (var item in items.EnumerateArray())
            {
                var itemKey = Text(item, "issueKey") ?? "";
                if (LongProp(item, "issueNumber") == number || itemKey.EndsWith($"-{number}", StringComparison.Ordinal)) return item.GetRawText();
            }
            return null;
        }

        var id = await ResolveIssueIdAsync(client, key, ct);
        return id.HasValue ? await ById(id.Value) : null;
    }
}


public class CheckEmployeeLeavesTool : BaseAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CheckEmployeeLeavesTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public override string Name => "check_employee_leaves";
    public override string Description => "List pending and approved leave and work-from-home requests that overlap a date range. Shows the signed-in user's own requests by default; scope 'team' covers their direct reports and 'all' covers the organisation and is limited to HR and Admin. Use it for who is away, availability, and sprint capacity.";
    public override string Category => "Persona";
    
    public override string RequiredPermission => "EMPLOYEE_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["startDate"] = new { type = "STRING", description = "Start of the range, YYYY-MM-DD. Defaults to today." },
            ["endDate"] = new { type = "STRING", description = "End of the range, YYYY-MM-DD. Defaults to 14 days after the start." },
            ["scope"] = new { type = "STRING", description = "'mine' (default), 'team' for direct reports, or 'all' for everyone." },
            ["employee"] = new { type = "STRING", description = "Only show this person's requests (a name or part of one). Use with scope 'team' or 'all'." },
            ["history"] = new { type = "BOOLEAN", description = "true for \"history\", \"all my requests\" or \"past leaves\": lists every request of the calendar year, past and upcoming, with any status (pending, approved, rejected, cancelled). Ignores startDate and endDate." },
            ["year"] = new { type = "INTEGER", description = "Calendar year for history. Defaults to the current year." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var history = arguments.TryGetValue("history", out var hv) && (hv is true || string.Equals(hv?.ToString(), "true", StringComparison.OrdinalIgnoreCase));
            
            if (Str(arguments, "employee") != null && Str(arguments, "startDate") == null && Str(arguments, "endDate") == null) history = true;
            var start = TryDate(Str(arguments, "startDate"), out var s) ? s : TodayLocal;
            var end = TryDate(Str(arguments, "endDate"), out var e) ? e : start.AddDays(14);
            if (history)
            {
                var historyYear = int.TryParse(Str(arguments, "year"), out var hy) && hy is >= 2000 and <= 2100 ? hy : TodayLocal.Year;
                start = new DateOnly(historyYear, 1, 1);
                end = new DateOnly(historyYear, 12, 31);
            }
            if (end < start) return new ToolResult(false, null, "endDate must not be before startDate.");
            if (end.DayNumber - start.DayNumber > 366) return new ToolResult(false, null, "Choose a range of at most one year.");

            var scope = (Str(arguments, "scope") ?? "mine").ToLowerInvariant();
            if (scope is not ("mine" or "team" or "all")) scope = "mine";
            
            if (scope == "mine" && Str(arguments, "employee") != null) scope = "all";

            
            var canSeeEveryone = InAnyRole(arguments, "ADMIN", "HR", "HR_ADMIN");
            string? scopeNote = null;
            if (scope == "all" && !canSeeEveryone)
            {
                scope = "team";
                scopeNote = "Only HR and Admin can see everyone's leave, so this covers the user's own direct reports only.";
            }
            var employeeFilter = Str(arguments, "employee")?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var flag = scope switch { "team" => "&team=true", "all" => "&all=true", _ => "" };

            var client = _httpClientFactory.CreateClient("IdentityTenantService");
            AttachBearerToken(client, _httpContextAccessor);

            var found = new List<object>();
            JsonElement? balances = null;

            async Task<HashSet<long>> PendingIds(string listFlag)
            {
                var ids = new HashSet<long>();
                for (var year = start.Year; year <= end.Year; year++)
                {
                    for (var page = 1; page <= 10; page++)
                    {
                        var listResponse = await client.GetAsync($"{IdentityRoutes.Absence}?year={year}{listFlag}&page={page}", cancellationToken);
                        if (!listResponse.IsSuccessStatusCode) break;
                        using var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken));
                        if (listDoc.RootElement.TryGetProperty("items", out var listItems) && listItems.ValueKind == JsonValueKind.Array)
                            foreach (var t in listItems.EnumerateArray())
                                if (Text(t, "status")?.ToUpperInvariant() == "PENDING" && LongProp(t, "id") is { } tid) ids.Add(tid);
                        var listPages = listDoc.RootElement.TryGetProperty("totalPages", out var tpp) && tpp.ValueKind == JsonValueKind.Number ? tpp.GetInt32() : 1;
                        if (page >= listPages) break;
                    }
                }
                return ids;
            }

            
            var decidable = new HashSet<long>();
            var ownIds = new HashSet<long>();
            var callerIsHr = InAnyRole(arguments, "HR");
            if (scope != "mine")
            {
                decidable = await PendingIds("&team=true");
                if (callerIsHr) ownIds = await PendingIds("");
            }
            bool? policyConfigured = null;

            
            for (var year = start.Year; year <= end.Year; year++)
            {
                for (var page = 1; page <= 10; page++)
                {
                    var response = await client.GetAsync($"{IdentityRoutes.Absence}?year={year}{flag}&page={page}", cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        return new ToolResult(false, null, $"Failed to fetch leave requests: {(int)response.StatusCode} {response.StatusCode}");

                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    var root = doc.RootElement;

                    if (balances == null && scope == "mine" && root.TryGetProperty("balances", out var b)) balances = b.Clone();
                    if (policyConfigured == null && root.TryGetProperty("policyConfigured", out var pc) && pc.ValueKind is JsonValueKind.True or JsonValueKind.False) policyConfigured = pc.GetBoolean();

                    if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            var status = Text(item, "status")?.ToUpperInvariant();
                            if (status is null || (!history && status is not ("APPROVED" or "PENDING"))) continue;
                            if (!DateOnly.TryParse(Text(item, "startDate"), CultureInfo.InvariantCulture, out var from)) continue;
                            if (!DateOnly.TryParse(Text(item, "endDate"), CultureInfo.InvariantCulture, out var to)) continue;
                            if (to < start || from > end) continue;

                            var who = Text(item, "employeeName") ?? "";
                            if (employeeFilter is { Length: > 0 } && !employeeFilter.All(t => who.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;

                            
                            found.Add(new
                            {
                                requestId = LongProp(item, "id"),
                                canDecide = status == "PENDING" && LongProp(item, "id") is { } rid && scope != "mine" && (decidable.Contains(rid) || (callerIsHr && !ownIds.Contains(rid))),
                                employeeName = Text(item, "employeeName"),
                                kind = Text(item, "kind"),
                                label = LeaveRules.Label(Text(item, "kind")),
                                startDate = from,
                                endDate = to,
                                days = item.TryGetProperty("days", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : (int?)null,
                                status
                            });
                        }
                    }

                    var totalPages = root.TryGetProperty("totalPages", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetInt32() : 1;
                    if (page >= totalPages) break;
                }
            }

            if (employeeFilter is { Length: > 0 } && !canSeeEveryone && found.Count == 0)
            {
                return new ToolResult(true, JsonSerializer.Serialize(new
                {
                    count = 0,
                    items = Array.Empty<object>(),
                    restricted = true,
                    message = $"No leave requests were found for '{Str(arguments, "employee")}' among the user's direct reports. Leave of people who do not report to the user is visible only to HR and Admin roles, so say that plainly and do not guess."
                }));
            }

            var labelled = balances.HasValue ? LeaveRules.Balances(balances.Value) : new List<object>();
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                from = start,
                to = end,
                scope,
                count = found.Count,
                items = found,
                balances = labelled,
                balanceYear = start.Year,
                policyConfigured,
                note = scopeNote,
                howToApply = "Pick the type (Floating holidays, Sick leave, Earned leave, Unpaid leave or Work from home) and the dates, and I will check your balance and submit the request to your manager for approval."
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class GetTimesheetSummaryTool : BaseAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetTimesheetSummaryTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public override string Name => "get_timesheet_summary";
    public override string Description => "Summarise the signed-in user's logged time for a date range: total, billable and non-billable minutes, daily totals, timesheet submission status, and the individual work logs. Defaults to the current week, Monday to Sunday.";
    public override string Category => "Chronos";
    public override string? RequiredPortal => "timesheet";
    
    public override string RequiredPermission => "TIMESHEET_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["fromDate"] = new { type = "STRING", description = "First day, YYYY-MM-DD. Defaults to this Monday." },
            ["toDate"] = new { type = "STRING", description = "Last day, YYYY-MM-DD. Defaults to six days after fromDate." },
            ["weekStartDate"] = new { type = "STRING", description = "Same as fromDate. Kept for older callers." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = TodayLocal;
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var from = TryDate(Str(arguments, "fromDate"), out var f) ? f
                : TryDate(Str(arguments, "weekStartDate"), out var w) ? w
                : monday;
            var to = TryDate(Str(arguments, "toDate"), out var t) ? t : from.AddDays(6);
            if (to < from) return new ToolResult(false, null, "toDate must not be before fromDate.");
            if (to.DayNumber - from.DayNumber > 366) return new ToolResult(false, null, "Choose a range of at most one year.");

            var client = _httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, _httpContextAccessor);

            var response = await client.GetAsync($"{JiraRoutes.MyTimesheet}?From={from:yyyy-MM-dd}&To={to:yyyy-MM-dd}&PageSize=50", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"Failed to get timesheet summary: {(int)response.StatusCode} {response.StatusCode}");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;

            var logs = new List<object>();
            if (root.TryGetProperty("workLogs", out var page) && page.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var log in items.EnumerateArray())
                {
                    logs.Add(new
                    {
                        issueKey = Text(log, "issueKey"),
                        issue = Text(log, "issueSummary"),
                        date = Text(log, "workDate"),
                        minutes = log.TryGetProperty("durationMinutes", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : 0,
                        billable = log.TryGetProperty("billable", out var bl) && bl.ValueKind == JsonValueKind.True,
                        description = Text(log, "description"),
                        status = Text(log, "status")
                    });
                }
            }

            var periods = new List<object>();
            if (root.TryGetProperty("periods", out var ps) && ps.ValueKind == JsonValueKind.Array)
            {
                foreach (var period in ps.EnumerateArray())
                {
                    periods.Add(new
                    {
                        start = Text(period, "periodStart"),
                        end = Text(period, "periodEnd"),
                        status = Text(period, "status"),
                        totalMinutes = period.TryGetProperty("totalMinutes", out var tm) && tm.ValueKind == JsonValueKind.Number ? tm.GetInt64() : 0
                    });
                }
            }

            long Minutes(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

            return new ToolResult(true, Cap(JsonSerializer.Serialize(new
            {
                from,
                to,
                totalMinutes = Minutes("totalMinutes"),
                billableMinutes = Minutes("billableMinutes"),
                nonBillableMinutes = Minutes("nonBillableMinutes"),
                dailyTotals = root.TryGetProperty("dailyTotals", out var days) ? days.Clone() : (JsonElement?)null,
                periods,
                workLogs = logs
            })));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class LogWorkTool : BaseAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LogWorkTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public override string Name => "log_work";
    public override string Description => "Log time worked against an issue on a given day, with a note that is kept on the work log. Needs the issue and how long; the date defaults to today. Convert hours to minutes (2.5 hours is 150). Ask the user if the issue or duration is missing instead of guessing.";
    public override string Category => "Chronos";
    public override string RequiredPermission => "WORKLOG_CREATE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["issueIdOrKey"] = new { type = "STRING", description = "The issue key (for example 'FLP360-17') or numeric issue ID." },
            ["durationMinutes"] = new { type = "INTEGER", description = "Time worked in minutes, 1 to 1440 (60 for 1h, 150 for 2.5h)." },
            ["workDate"] = new { type = "STRING", description = "Day the work was done, YYYY-MM-DD. Defaults to today." },
            ["description"] = new { type = "STRING", description = "What was done. Stored as the comment on the work log." },
            ["startTime"] = new { type = "STRING", description = "When the work started, HH:mm (24-hour). Only if the user said it; never invent one." },
            ["billable"] = new { type = "BOOLEAN", description = "Whether the time is billable. Defaults to true." }
        },
        required = new[] { "issueIdOrKey", "durationMinutes" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var idOrKey = Str(arguments, "issueIdOrKey") ?? Str(arguments, "issueId");
            if (idOrKey == null)
                return new ToolResult(false, null, "Which issue should the time be logged against? An issue key or ID is required.");

            var minutes = Long(arguments, "durationMinutes");
            if (minutes is not (>= 1 and <= 1440))
                return new ToolResult(false, null, "durationMinutes is required and must be between 1 and 1440.");

            var workDate = TodayLocal;
            if (Str(arguments, "workDate") is { } given)
            {
                if (!TryDate(given, out workDate))
                    return new ToolResult(false, null, "workDate must be in YYYY-MM-DD format.");
            }

            TimeOnly? start = null;
            TimeOnly? end = null;
            if (Str(arguments, "startTime") is { } startText)
            {
                if (!TimeOnly.TryParseExact(startText, ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
                    return new ToolResult(false, null, "startTime must be written as HH:mm, for example 14:30.");
                start = parsedStart;
                
                if (parsedStart.ToTimeSpan().TotalMinutes + minutes.Value < 24 * 60) end = parsedStart.AddMinutes((int)minutes.Value);
            }

            var client = _httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, _httpContextAccessor);

            var issueId = await ResolveIssueIdAsync(client, idOrKey, cancellationToken);
            if (issueId == null)
                return new ToolResult(false, null, $"Issue '{idOrKey}' was not found, or you do not have access to it.");

            var payload = new Dictionary<string, object?>
            {
                ["issueId"] = issueId.Value,
                ["workDate"] = workDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["durationMinutes"] = (int)minutes.Value,
                ["description"] = Str(arguments, "description"),
                ["billable"] = Bool(arguments, "billable", true)
            };
            if (start.HasValue) payload["startTime"] = start.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            if (end.HasValue) payload["endTime"] = end.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            var response = await client.PostAsJsonAsync(JiraRoutes.WorkLogs, payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"The work was not logged: {ApiMessage(content, (int)response.StatusCode)}");

            using var doc = JsonDocument.Parse(content);
            var log = doc.RootElement;
            var logged = LongProp(log, "durationMinutes") ?? minutes.Value;
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                logged = true,
                workLogId = LongProp(log, "id"),
                issueKey = Text(log, "issueKey") ?? idOrKey,
                issueSummary = Text(log, "issueSummary"),
                workDate = Text(log, "workDate") ?? workDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                minutes = logged,
                hours = Hours(logged),
                billable = payload["billable"],
                comment = Text(log, "description") ?? Str(arguments, "description"),
                startTime = Text(log, "startTime") ?? start?.ToString("HH:mm", CultureInfo.InvariantCulture),
                endTime = Text(log, "endTime") ?? end?.ToString("HH:mm", CultureInfo.InvariantCulture),
                timesheetStatus = Text(log, "periodStatus")
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class PrepareWorkLogTool : BaseAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PrepareWorkLogTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public override string Name => "prepare_work_log";
    public override string Description => "Show the user a Log time form in the chat: their own open issues to pick from, quick duration buttons, a date and a note. Call this when the user wants to log time or work but has not given BOTH the issue and how long. Pass issueIdOrKey only if they named an issue. The form is the answer, so reply with one short sentence and never list the details they would have to type.";
    public override string Category => "Chronos";
    public override string RequiredPermission => "WORKLOG_CREATE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["issueIdOrKey"] = new { type = "STRING", description = "An issue the user already named, to preselect it in the form." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;
            var caller = CallerUserId(arguments);
            if (caller == null)
                return new ToolResult(false, null, "I can't tell who you are, so I can't open the time form. Please sign in again.");

            var client = _httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, _httpContextAccessor);

            var response = await client.GetAsync($"{JiraRoutes.IssueSearch}?Page=1&PageSize=50&AssigneeUserId={caller.Value}", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"Your issues could not be loaded: {ApiMessage(content, (int)response.StatusCode)}");
            content = KeepAssignedTo(content, caller.Value);

            var issues = new List<object>();
            var ids = new HashSet<long>();
            void Add(JsonElement issue)
            {
                if (LongProp(issue, "id") is not { } id || !ids.Add(id)) return;
                issues.Add(new
                {
                    id,
                    issueKey = Text(issue, "issueKey"),
                    summary = Text(issue, "summary"),
                    statusCode = Text(issue, "statusCode"),
                    priority = Text(issue, "priority")
                });
            }

            using var search = JsonDocument.Parse(content);
            if (search.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var status = Text(item, "statusCode");
                    
                    if (status is "DONE" or "CLOSED" or "RESOLVED" or "CANCELLED") continue;
                    Add(item);
                }
            }

            long? selected = null;
            if (Str(arguments, "issueIdOrKey") is { } named)
            {
                var issueId = await ResolveIssueIdAsync(client, named, cancellationToken);
                if (issueId == null)
                    return new ToolResult(false, null, $"Issue '{named}' was not found, or you do not have access to it.");
                var one = await client.GetAsync(JiraRoutes.Issue(issueId.Value), cancellationToken);
                if (!one.IsSuccessStatusCode)
                    return new ToolResult(false, null, $"Issue '{named}' was not found, or you do not have access to it.");
                using var doc = JsonDocument.Parse(await one.Content.ReadAsStringAsync(cancellationToken));
                if (owner.HasValue && !AssignedTo(doc.RootElement, owner.Value))
                    return new ToolResult(false, null, $"Issue '{named}' was not found, or it is not assigned to you.");
                if (!ids.Contains(issueId.Value))
                {
                    
                    var before = issues.ToList();
                    issues.Clear();
                    ids.Clear();
                    Add(doc.RootElement);
                    foreach (var existing in before) issues.Add(existing);
                }
                selected = issueId.Value;
            }

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                workLogForm = true,
                today = TodayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                selectedIssueId = selected,
                issues,
                note = issues.Count == 0 ? "You have no open issues assigned to you. Name an issue to log time against it." : null
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class TransitionIssueTool : BaseAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TransitionIssueTool(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public override string Name => "transition_issue";
    public override string Description => "Move an issue along its lifecycle: TODO, IN_PROGRESS, CODE_REVIEW, TESTING, DONE. Accepts a key such as 'FLP360-17' or 'P2-1', or a numeric id. The tool reads the issue's current version itself and sends it, so never ask the user for one.";
    public override string Category => "GridHub";
    public override string RequiredPermission => "ISSUE_TRANSITION";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["issueIdOrKey"] = new { type = "STRING", description = "Issue key ('FLP360-17', 'P2-1') or numeric id." },
            ["statusCode"] = new { type = "STRING", description = "TODO, IN_PROGRESS, CODE_REVIEW, TESTING or DONE." },
            ["confirmSkip"] = new { type = "BOOLEAN", description = "Allow skipping workflow steps (default true)." }
        },
        required = new[] { "issueIdOrKey", "statusCode" }
    };

    private static readonly Dictionary<string, string> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TODO"] = "TODO", ["TO_DO"] = "TODO", ["BACKLOG"] = "TODO", ["OPEN"] = "TODO", ["REOPEN"] = "TODO", ["REOPENED"] = "TODO",
        ["IN_PROGRESS"] = "IN_PROGRESS", ["INPROGRESS"] = "IN_PROGRESS", ["PROGRESS"] = "IN_PROGRESS", ["STARTED"] = "IN_PROGRESS", ["DOING"] = "IN_PROGRESS", ["START"] = "IN_PROGRESS",
        ["CODE_REVIEW"] = "CODE_REVIEW", ["IN_REVIEW"] = "CODE_REVIEW", ["REVIEW"] = "CODE_REVIEW", ["PEER_REVIEW"] = "CODE_REVIEW",
        ["TESTING"] = "TESTING", ["IN_TESTING"] = "TESTING", ["TEST"] = "TESTING", ["QA"] = "TESTING",
        ["DONE"] = "DONE", ["COMPLETE"] = "DONE", ["COMPLETED"] = "DONE", ["FINISHED"] = "DONE", ["CLOSED"] = "DONE", ["RESOLVED"] = "DONE"
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var idOrKey = Str(arguments, "issueIdOrKey") ?? "";
            var requested = Str(arguments, "statusCode") ?? "";
            if (idOrKey.Length == 0) return new ToolResult(false, null, "issueIdOrKey is required.");
            if (requested.Length == 0) return new ToolResult(false, null, "statusCode is required.");

            var confirmSkip = Bool(arguments, "confirmSkip", true);

            
            var normalised = requested.ToUpperInvariant().Replace(' ', '_').Replace('-', '_');
            var statusCode = StatusAliases.TryGetValue(normalised, out var mapped) ? mapped : normalised;

            var (owner, denied) = IssueScope(arguments);
            if (denied != null) return denied;

            var client = _httpClientFactory.CreateClient("JiraService");
            AttachBearerToken(client, _httpContextAccessor);

            var resolvedId = await ResolveIssueIdAsync(client, idOrKey, cancellationToken);
            if (resolvedId == null)
                return new ToolResult(false, null, $"Issue '{idOrKey}' was not found, or you do not have access to it.");
            var issueId = resolvedId.Value;

            
            var issueResponse = await client.GetAsync(JiraRoutes.Issue(issueId), cancellationToken);
            if (!issueResponse.IsSuccessStatusCode)
            {
                return new ToolResult(false, null, owner.HasValue
                    ? $"Issue '{idOrKey}' was not found, or it is not assigned to you."
                    : $"Could not read issue '{idOrKey}' to get its current version: {ApiMessage(await issueResponse.Content.ReadAsStringAsync(cancellationToken), (int)issueResponse.StatusCode)}");
            }

            using var current = JsonDocument.Parse(await issueResponse.Content.ReadAsStringAsync(cancellationToken));
            var before = current.RootElement;
            
            if (owner.HasValue && !AssignedTo(before, owner.Value))
                return new ToolResult(false, null, $"Issue '{idOrKey}' was not found, or it is not assigned to you.");

            var version = LongProp(before, "version");
            if (version == null)
                return new ToolResult(false, null, $"Issue '{idOrKey}' did not report a version, so the move was not attempted.");
            var previousStatus = Text(before, "statusCode");
            var issueKey = Text(before, "issueKey") ?? idOrKey;

            if (string.Equals(previousStatus, statusCode, StringComparison.OrdinalIgnoreCase))
                return new ToolResult(false, null, $"{issueKey} is already {statusCode}.");

            using var request = new HttpRequestMessage(HttpMethod.Put, JiraRoutes.IssueTransition(issueId))
            {
                Content = JsonContent.Create(new { statusCode, confirmSkip })
            };
            request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{version}\""));

            var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, $"{issueKey} was not moved to {statusCode}: {ApiMessage(content, (int)response.StatusCode)}");

            JsonElement after = default;
            try { after = JsonDocument.Parse(content).RootElement; } catch (JsonException) { }

            
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                transitioned = true,
                id = issueId,
                issueKey,
                summary = Text(after, "summary") ?? Text(before, "summary"),
                previousStatus,
                statusCode = Text(after, "statusCode") ?? statusCode,
                priority = Text(after, "priority") ?? Text(before, "priority"),
                version = LongProp(after, "version") ?? version + 1
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}

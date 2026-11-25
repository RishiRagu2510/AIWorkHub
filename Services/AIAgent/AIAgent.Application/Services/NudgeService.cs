using System.Globalization;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace AIAgent.Application.Services;


public sealed record Nudge(string Id, string Severity, string Title, string Detail, string Prompt);

public sealed record NudgeReport(IReadOnlyList<Nudge> Items, DateTimeOffset GeneratedAt);

public sealed class NudgeService(
    IEnumerable<IAgentTool> tools,
    ICallerIdentityProvider identity,
    IMemoryCache? cache = null,
    Func<DateOnly>? today = null)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(2);

    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";

    private sealed record Rule(string Tool, Dictionary<string, object?> Arguments, Func<JsonElement, DateOnly, IEnumerable<Nudge>> Read);

    
    public async Task<NudgeReport?> GetAsync(CancellationToken cancellationToken = default)
    {
        var caller = identity.HasCredentials ? await identity.VerifyAsync(cancellationToken) : null;
        if (caller is not { UserId: > 0 }) return null;

        var key = $"nudges:{caller.TenantId}:{caller.UserId}";
        if (cache is not null && cache.TryGetValue(key, out NudgeReport? cached) && cached is not null) return cached;

        var roles = caller.Roles.Count > 0 ? caller.Roles : [caller.Role];
        var isAdmin = roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase));
        var day = (today ?? (() => DateOnly.FromDateTime(DateTime.Now)))();

        var found = await Task.WhenAll(Rules().Select(rule => RunAsync(rule, caller, roles, isAdmin, day, cancellationToken)));
        var items = found.SelectMany(x => x)
            .Select((nudge, order) => (nudge, order))
            .OrderBy(x => x.nudge.Severity == High ? 0 : x.nudge.Severity == Medium ? 1 : 2).ThenBy(x => x.order)
            .Select(x => x.nudge).ToList();

        var report = new NudgeReport(items, DateTimeOffset.UtcNow);
        cache?.Set(key, report, CacheFor);
        return report;
    }

    private async Task<IReadOnlyList<Nudge>> RunAsync(Rule rule, VerifiedCaller caller, IReadOnlyCollection<string> roles, bool isAdmin, DateOnly day, CancellationToken cancellationToken)
    {
        var tool = tools.FirstOrDefault(t => t.Name == rule.Tool);
        if (tool is null || !ToolLicense.IsAvailable(tool, caller.AllowedPortals)) return [];
        if (!string.IsNullOrWhiteSpace(tool.RequiredPermission) && !isAdmin && !caller.Permissions.Contains(tool.RequiredPermission, StringComparer.OrdinalIgnoreCase)) return [];

        try
        {
            var result = await tool.ExecuteAsync(IssueVisibility.ArgumentsFor(rule.Arguments, roles, caller.UserId), cancellationToken);
            if (!result.Success) return [];
            using var document = result.Data switch
            {
                string text when !string.IsNullOrWhiteSpace(text) => JsonDocument.Parse(text),
                JsonElement element => JsonDocument.Parse(element.GetRawText()),
                null => null,
                var other => JsonDocument.Parse(JsonSerializer.Serialize(other))
            };
            return document is null ? [] : rule.Read(document.RootElement, day).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IEnumerable<Rule> Rules() =>
    [
        new("get_work_digest", new(), WorkNudges),
        new("get_timesheet_history", new() { ["weeks"] = 2 }, TimesheetNudges),
        new("approve_timesheet_period", new() { ["action"] = "list" }, TimesheetReviewNudges),
        new("approve_leave_request", new() { ["action"] = "list" }, LeaveDecisionNudges)
    ];

    

    private static IEnumerable<Nudge> WorkNudges(JsonElement root, DateOnly day)
    {
        if (!root.TryGetProperty("counts", out var counts)) yield break;
        var overdue = Count(counts, "overdue");
        var soon = Count(counts, "dueWithin7Days");

        
        string? First(string wanted) =>
            root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Where(i => Text(i, "why")?.StartsWith(wanted, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(i => $"{Text(i, "issueKey")} {Text(i, "summary")}".Trim()).FirstOrDefault()
                : null;

        if (overdue > 0)
            yield return new("overdue", High, $"{overdue} overdue {Plural(overdue, "issue")}",
                First("overdue") is { } top ? $"Most pressing: {top}" : "Past their due date and still open.", "What should I work on today?");
        if (soon > 0)
            yield return new("due-soon", Medium, $"{soon} {Plural(soon, "issue")} due in the next 7 days",
                First("due") is { } next ? $"Next up: {next}" : "Coming up this week.", "What should I work on today?");
    }

    private static IEnumerable<Nudge> TimesheetNudges(JsonElement root, DateOnly day)
    {
        if (!root.TryGetProperty("weeks", out var weeks) || weeks.ValueKind != JsonValueKind.Array) yield break;
        var list = weeks.EnumerateArray().ToList();   
        if (list.Count == 0) yield break;

        var thisMonday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        JsonElement? Week(DateOnly monday) => list.Cast<JsonElement?>().FirstOrDefault(w => w is { } x && Text(x, "weekStart") == monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        
        if (Week(thisMonday.AddDays(-7)) is { } last && Number(last, "targetHours") > 0)
        {
            var status = Text(last, "status");
            var missing = Number(last, "missingHours");
            if (status == "REJECTED")
                yield return new("timesheet-rejected", High, "Last week's timesheet was sent back",
                    Text(last, "rejectionReason") is { Length: > 0 } reason ? $"Reason given: {reason}" : "Fix it and submit it again.", "Show my timesheet history");
            else if (status is "DRAFT" or "NOT_STARTED")
                yield return new("timesheet-last-week", High, "Last week's timesheet is not submitted",
                    missing > 0 ? $"{Hours(missing)} still missing against the 8 hour days." : "All the hours are logged; it only needs submitting.", "Show my timesheet history");
        }

        
        if (day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday
            && Week(thisMonday) is { } current && Text(current, "status") == "DRAFT" && Number(current, "loggedHours") > 0)
            yield return new("timesheet-this-week", Medium, "This week's timesheet is ready to submit",
                $"{Hours(Number(current, "loggedHours"))} logged so far.", "Submit my timesheet for this week");

        
        var yesterday = day.AddDays(-1);
        if (yesterday.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && Week(yesterday.AddDays(-(((int)yesterday.DayOfWeek + 6) % 7))) is { } week
            && week.TryGetProperty("days", out var days) && days.ValueKind == JsonValueKind.Array
            && days.EnumerateArray().FirstOrDefault(d => Text(d, "date") == yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) is { ValueKind: JsonValueKind.Object } y
            && Text(y, "note") is null && Number(y, "targetHours") > 0 && Number(y, "loggedHours") == 0)
            yield return new("log-yesterday", Low, $"No time logged for {yesterday.DayOfWeek}", "Log it while you still remember what you worked on.", "I want to log time for yesterday");
    }

    private static IEnumerable<Nudge> TimesheetReviewNudges(JsonElement root, DateOnly day)
    {
        var pending = Count(root, "pendingCount");
        if (pending > 0)
            yield return new("timesheets-to-review", Medium, $"{pending} {Plural(pending, "timesheet")} waiting for your review",
                "Submitted by your team and not yet decided.", "Show the timesheets waiting for my approval");
    }

    private static IEnumerable<Nudge> LeaveDecisionNudges(JsonElement root, DateOnly day)
    {
        var pending = Count(root, "pendingCount");
        if (pending <= 0) yield break;

        
        var nearest = root.TryGetProperty("requests", out var requests) && requests.ValueKind == JsonValueKind.Array
            ? requests.EnumerateArray().Select(r => (Name: Text(r, "employee"), Start: DateOnly.TryParse(Text(r, "startDate"), CultureInfo.InvariantCulture, out var s) ? s : (DateOnly?)null))
                .Where(r => r.Start.HasValue && r.Start >= day).OrderBy(r => r.Start).Cast<(string? Name, DateOnly? Start)?>().FirstOrDefault()
            : null;
        var soonest = nearest is { Start: { } start } n && start <= day.AddDays(7);

        yield return new("leave-to-decide", soonest ? High : Medium, $"{pending} leave {Plural(pending, "request")} waiting for your decision",
            nearest is { } near ? $"Earliest starts {near.Start:MMM d}{(string.IsNullOrWhiteSpace(near.Name) ? "" : $" ({near.Name})")}." : "Your team is waiting for an answer.",
            "Show the pending leave requests I need to approve");
    }

    

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double Number(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static long Count(JsonElement e, string name) => (long)Number(e, name);

    private static string Plural(long count, string word) => count == 1 ? word : word + "s";

    private static string Hours(double hours) => $"{Math.Round(hours, 1).ToString("0.#", CultureInfo.InvariantCulture)} h";
}

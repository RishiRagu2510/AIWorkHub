using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;


public abstract class WorkLogToolBase(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    protected const int WeeklyTargetHours = 5 * 8;

    protected static ToolResult Fail(string message) => new(false, null, message);

    
    public static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    
    protected static bool IsLocked(string? periodStatus) => periodStatus is "SUBMITTED" or "APPROVED";

    protected static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected static string HoursText(long minutes) => $"{Hours(minutes).ToString("0.##", CultureInfo.InvariantCulture)} h";

    
    protected static async Task<(JsonDocument? Doc, string? Error)> ReadAsync(HttpClient client, string url, string what, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return (null, $"{what}: {ApiMessage(body, (int)response.StatusCode)}");
        try { return (JsonDocument.Parse(body), null); }
        catch (JsonException) { return (null, $"{what}: the service sent an answer I could not read."); }
    }

    protected static IEnumerable<JsonElement> Items(JsonElement page) =>
        page.ValueKind == JsonValueKind.Object && page.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];

    protected static object Shape(JsonElement log)
    {
        var status = Text(log, "periodStatus") ?? Text(log, "status");
        var minutes = LongProp(log, "durationMinutes") ?? 0;
        return new
        {
            workLogId = LongProp(log, "id"),
            issueKey = Text(log, "issueKey"),
            summary = Text(log, "issueSummary"),
            project = Text(log, "projectName"),
            workDate = Text(log, "workDate"),
            minutes,
            hours = Hours(minutes),
            billable = log.TryGetProperty("billable", out var b) && b.ValueKind == JsonValueKind.True,
            note = Text(log, "description"),
            startTime = Text(log, "startTime"),
            timesheetStatus = status,
            canEdit = !IsLocked(status)
        };
    }

    protected static bool IsOwnedBy(JsonElement log, long owner) => LongProp(log, "userId") is not { } id || id == owner;
}


public class GetWorkLogsTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : WorkLogToolBase(factory, accessor)
{
    private const int Shown = 40;

    public override string Name => "get_work_logs";
    public override string Description =>
        "List the signed-in user's own work logs (time entries) for a day or a date range, newest first. Each has a workLogId, the issue, hours, the note, and canEdit (false once that week is submitted or approved). " +
        "Use it for 'what did I log yesterday', 'what did I log this week' or 'show my entries for FLP360-17'. Pass the same date as fromDate and toDate for one day. With no dates it shows this week so far. " +
        "Never use it to answer how many hours are missing; get_timesheet_history does that.";
    public override string Category => "Chronos";
    public override string RequiredPermission => "WORKLOG_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["fromDate"] = new { type = "STRING", description = "First day, YYYY-MM-DD." },
            ["toDate"] = new { type = "STRING", description = "Last day, YYYY-MM-DD. Same as fromDate for a single day." },
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
            if (gotFrom == null && gotTo == null) { from = MondayOf(today); to = today; }
            else if (gotTo == null) to = from;
            else if (gotFrom == null) from = to;
            if (from > to) return Fail("fromDate is after toDate.");
            if (to.DayNumber - from.DayNumber > 365) return Fail("I can read at most a year of entries at a time.");

            var jira = Jira();
            var url = $"{JiraRoutes.WorkLogs}?From={Day(from)}&To={Day(to)}&UserId={caller}&Page=1&PageSize=100";
            if (Str(arguments, "issueIdOrKey") is { } key)
            {
                var issueId = await ResolveIssueIdAsync(jira, key, cancellationToken);
                if (issueId == null) return Fail($"Issue '{key}' was not found, or you do not have access to it.");
                url += $"&IssueId={issueId}";
            }

            var (doc, error) = await ReadAsync(jira, url, "I could not read your time entries", cancellationToken);
            using var _ = doc;
            if (doc == null) return Fail(error!);

            var logs = Items(doc.RootElement)
                .Where(l => IsOwnedBy(l, caller))
                .OrderByDescending(l => Text(l, "workDate"), StringComparer.Ordinal)
                .ThenByDescending(l => LongProp(l, "id"))
                .ToList();
            var total = logs.Sum(l => LongProp(l, "durationMinutes") ?? 0);
            var totalCount = doc.RootElement.TryGetProperty("totalCount", out var tc) && tc.ValueKind == JsonValueKind.Number ? tc.GetInt32() : logs.Count;

            string? note = null;
            if (totalCount > logs.Count) note = "Only the first 100 entries were read, so the total may be too low. Ask for a shorter range.";
            else if (logs.Count > Shown) note = $"Showing the newest {Shown} of {logs.Count} entries. The total covers all {logs.Count}.";
            else if (logs.Count == 0) note = "Nothing is logged in that range.";

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                from = Day(from),
                to = Day(to),
                count = logs.Count,
                totalHours = Hours(total),
                logs = logs.Take(Shown).Select(Shape).ToArray(),
                note
            }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}


public class UpdateWorkLogTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : WorkLogToolBase(factory, accessor)
{
    public override string Name => "update_work_log";
    public override string Description =>
        "Change one of the signed-in user's own time entries: how long (durationMinutes), the note (description), billable, or the day it belongs to (moveToDate). " +
        "Name the entry with workLogId (from get_work_logs), or with issueIdOrKey plus workDate (the day it was logged, default today). If that matches several entries the tool lists them: ask the user which one. " +
        "Convert hours to minutes (2.5 hours is 150). Pass only what the user asked to change. Entries in a week that was already submitted or approved are locked and cannot be changed. " +
        "To remove an entry use prepare_delete. Only do this when the user clearly asked for the change.";
    public override string Category => "Chronos";
    public override string RequiredPermission => "WORKLOG_UPDATE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["workLogId"] = new { type = "INTEGER", description = "The entry's id, from get_work_logs." },
            ["issueIdOrKey"] = new { type = "STRING", description = "The issue the entry was logged on, when there is no workLogId." },
            ["workDate"] = new { type = "STRING", description = "The day the entry was logged, YYYY-MM-DD, when there is no workLogId. Defaults to today." },
            ["durationMinutes"] = new { type = "INTEGER", description = "The new time in minutes, 1 to 1440." },
            ["description"] = new { type = "STRING", description = "The new note. Replaces the old one." },
            ["billable"] = new { type = "BOOLEAN", description = "Whether the time is billable." },
            ["moveToDate"] = new { type = "STRING", description = "Move the entry to this day, YYYY-MM-DD." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (CallerUserId(arguments) is not { } caller)
                return Fail("I can't tell who you are, so I can't change your time entries. Please sign in again.");

            
            long? newMinutes = null;
            if (Long(arguments, "durationMinutes") is { } minutes)
            {
                if (minutes is < 1 or > 1440) return Fail("durationMinutes must be between 1 and 1440.");
                newMinutes = minutes;
            }
            var newNote = Str(arguments, "description");
            var newBillable = arguments.ContainsKey("billable") && arguments["billable"] != null ? Bool(arguments, "billable", true) : (bool?)null;
            DateOnly? newDay = null;
            if (Str(arguments, "moveToDate") is { } moveText)
            {
                if (!TryDate(moveText, out var moved)) return Fail("moveToDate must be written as YYYY-MM-DD.");
                newDay = moved;
            }
            if (newMinutes == null && newNote == null && newBillable == null && newDay == null)
                return Fail("What should change: the time, the note, billable, or the day? Ask the user.");

            
            var jira = Jira();
            JsonDocument? found = null;
            try
            {
                JsonElement log;
                if (Long(arguments, "workLogId") is { } id)
                {
                    var (doc, error) = await ReadAsync(jira, JiraRoutes.WorkLog(id), $"Work log {id} could not be read", cancellationToken);
                    found = doc;
                    if (doc == null) return Fail($"Time entry {id} was not found, or it is not yours. ({error})");
                    log = doc.RootElement;
                    if (!IsOwnedBy(log, caller)) return Fail($"Time entry {id} was not found, or it is not yours.");
                }
                else
                {
                    var idOrKey = Str(arguments, "issueIdOrKey");
                    if (idOrKey == null) return Fail("Which entry? Give its workLogId from get_work_logs, or the issue and the day it was logged.");
                    var day = TodayLocal;
                    if (Str(arguments, "workDate") is { } dayText && !TryDate(dayText, out day)) return Fail("workDate must be written as YYYY-MM-DD.");

                    var issueId = await ResolveIssueIdAsync(jira, idOrKey, cancellationToken);
                    if (issueId == null) return Fail($"Issue '{idOrKey}' was not found, or you do not have access to it.");

                    var (doc, error) = await ReadAsync(jira, $"{JiraRoutes.WorkLogs}?From={Day(day)}&To={Day(day)}&UserId={caller}&IssueId={issueId}&Page=1&PageSize=50", "I could not read your time entries", cancellationToken);
                    found = doc;
                    if (doc == null) return Fail(error!);

                    var matches = Items(doc.RootElement).Where(l => IsOwnedBy(l, caller) && (LongProp(l, "issueId") is not { } li || li == issueId)).ToList();
                    if (matches.Count == 0) return Fail($"You have no time logged on {idOrKey} for {Day(day)}.");
                    if (matches.Count > 1)
                    {
                        var lines = matches.Select(l => $"workLogId {LongProp(l, "id")}: {HoursText(LongProp(l, "durationMinutes") ?? 0)}{(Text(l, "description") is { Length: > 0 } d ? $" \"{(d.Length > 50 ? d[..50] + "..." : d)}\"" : "")}");
                        return Fail($"You have {matches.Count} entries on {idOrKey} for {Day(day)}: {string.Join("; ", lines)}. Ask the user which one, then call update_work_log again with its workLogId.");
                    }
                    log = matches[0];
                }

                
                var status = Text(log, "periodStatus") ?? Text(log, "status");
                var key = Text(log, "issueKey") ?? Str(arguments, "issueIdOrKey") ?? "that issue";
                if (IsLocked(status))
                    return Fail($"That entry belongs to a week whose timesheet is {status}, so it can't be changed.");

                var logId = LongProp(log, "id");
                var version = LongProp(log, "version");
                var issueIdOfLog = LongProp(log, "issueId");
                if (logId == null || version == null || issueIdOfLog == null)
                    return Fail("That entry did not report what I need to change it safely, so I left it alone.");

                
                var oldMinutes = LongProp(log, "durationMinutes") ?? 0;
                var oldNote = Text(log, "description");
                var oldBillable = log.TryGetProperty("billable", out var bill) && bill.ValueKind == JsonValueKind.True;
                var oldDay = TryDate(Text(log, "workDate"), out var parsedDay) ? parsedDay : default;

                var changes = new List<object>();
                if (newMinutes is { } nm && nm != oldMinutes) changes.Add(new { field = "time", from = HoursText(oldMinutes), to = HoursText(nm) });
                if (newNote != null && newNote != (oldNote ?? "")) changes.Add(new { field = "note", from = oldNote ?? "", to = newNote });
                if (newBillable is { } nb && nb != oldBillable) changes.Add(new { field = "billable", from = oldBillable ? "yes" : "no", to = nb ? "yes" : "no" });
                if (newDay is { } nd && nd != oldDay) changes.Add(new { field = "day", from = Day(oldDay), to = Day(nd) });
                if (changes.Count == 0) return Fail($"That entry on {key} already looks like that, so nothing was changed.");

                var finalMinutes = newMinutes ?? oldMinutes;
                var finalDay = newDay ?? oldDay;

                
                TimeOnly? start = TimeOnly.TryParse(Text(log, "startTime"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var s) ? s : null;
                TimeOnly? end = null;
                if (start is { } st && st.ToTimeSpan().TotalMinutes + finalMinutes < 24 * 60) end = st.AddMinutes((int)finalMinutes);

                var body = new Dictionary<string, object?>
                {
                    ["issueId"] = issueIdOfLog.Value,
                    ["workDate"] = Day(finalDay),
                    ["durationMinutes"] = (int)finalMinutes,
                    ["description"] = newNote ?? oldNote,
                    ["billable"] = newBillable ?? oldBillable
                };
                if (start.HasValue) body["startTime"] = start.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                if (end.HasValue) body["endTime"] = end.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

                using var request = new HttpRequestMessage(HttpMethod.Put, JiraRoutes.WorkLog(logId)) { Content = JsonContent.Create(body) };
                request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
                using var response = await jira.SendAsync(request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return Fail($"The time entry was not changed: {ApiMessage(content, (int)response.StatusCode)}");

                JsonElement after = default;
                try { after = JsonDocument.Parse(content).RootElement; } catch (JsonException) { }

                return new ToolResult(true, JsonSerializer.Serialize(new
                {
                    updated = true,
                    workLogId = logId,
                    issueKey = Text(after, "issueKey") ?? key,
                    summary = Text(after, "issueSummary") ?? Text(log, "issueSummary"),
                    workDate = Text(after, "workDate") ?? Day(finalDay),
                    minutes = LongProp(after, "durationMinutes") ?? finalMinutes,
                    hours = Hours(LongProp(after, "durationMinutes") ?? finalMinutes),
                    note = Text(after, "description") ?? newNote ?? oldNote,
                    changes
                }));
            }
            finally
            {
                found?.Dispose();
            }
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}


public class SubmitTimesheetTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : WorkLogToolBase(factory, accessor)
{
    public override string Name => "submit_timesheet";
    public override string Description =>
        "Submit the signed-in user's own weekly timesheet (Monday to Sunday) to a reviewer. Pass weekStart as any date in the week (default: this week) and approver as the reviewer's name the user chose. " +
        "If no approver is given the tool lists the eligible reviewers: show them and ask which one, never pick for the user. A name that fits several people is refused with the list. " +
        "A week with nothing logged, or one already submitted or approved, is refused. A rejected week can be submitted again. Only do this when the user clearly asked to submit. " +
        "Mention loggedHours against weeklyTargetHours when the week is short.";
    public override string Category => "Chronos";
    public override string? RequiredPortal => "timesheet";
    public override string RequiredPermission => "TIMESHEET_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["weekStart"] = new { type = "STRING", description = "Any date in the week to submit, YYYY-MM-DD. Defaults to this week." },
            ["approver"] = new { type = "STRING", description = "The reviewer's name (or numeric userId), as the user chose it." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (CallerUserId(arguments) == null)
                return Fail("I can't tell who you are, so I can't submit a timesheet for you. Please sign in again.");

            var day = TodayLocal;
            if (Str(arguments, "weekStart") is { } dayText && !TryDate(dayText, out day)) return Fail("weekStart must be written as YYYY-MM-DD.");
            var monday = MondayOf(day);
            var sunday = monday.AddDays(6);

            var jira = Jira();
            var (report, readError) = await ReadAsync(jira, $"{JiraRoutes.MyTimesheet}?From={Day(monday)}&To={Day(sunday)}&PageSize=1", "I could not read your timesheet", cancellationToken);
            using var reportDoc = report;
            if (report == null) return Fail(readError!);

            JsonElement period = default;
            if (report.RootElement.TryGetProperty("periods", out var periods) && periods.ValueKind == JsonValueKind.Array)
                foreach (var p in periods.EnumerateArray())
                    if (Text(p, "periodStart") == Day(monday)) { period = p; break; }

            var total = period.ValueKind == JsonValueKind.Object ? LongProp(period, "totalMinutes") ?? 0 : 0;
            if (period.ValueKind != JsonValueKind.Object || total == 0)
                return Fail($"Nothing is logged for the week of {Day(monday)}, so there is nothing to submit.");

            var status = Text(period, "status") ?? "DRAFT";
            if (status == "SUBMITTED") return Fail($"The week of {Day(monday)} was already submitted{(Text(period, "submittedAt") is { } at ? $" on {at[..Math.Min(10, at.Length)]}" : "")} and is waiting for review.");
            if (status == "APPROVED") return Fail($"The week of {Day(monday)} is already approved.");

            var periodId = LongProp(period, "id");
            var version = LongProp(period, "version");
            if (periodId == null || version == null) return Fail("That week did not report what I need to submit it safely, so I did not send it.");

            var (approverDoc, approverError) = await ReadAsync(jira, JiraRoutes.TimesheetApprovers(periodId), "I could not look up who can review it", cancellationToken);
            using var approversDoc = approverDoc;
            if (approverDoc == null) return Fail(approverError!);

            var reviewers = new List<AssigneeCandidate>();
            if (approverDoc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var a in approverDoc.RootElement.EnumerateArray())
                    if (LongProp(a, "id") is { } reviewerId) reviewers.Add(new AssigneeCandidate(reviewerId, Text(a, "displayName"), null));
            if (reviewers.Count == 0)
                return Fail("No one is eligible to review this week: a reviewer needs access to every issue in it. Tell the user to ask an admin or their manager.");

            string Names(IEnumerable<AssigneeCandidate> people) => string.Join(", ", people.Take(15).Select(c => c.Name ?? $"user {c.UserId}"));

            var said = Str(arguments, "approver") ?? (Long(arguments, "approverUserId") is { } n ? n.ToString(CultureInfo.InvariantCulture) : null);
            if (said == null)
                return Fail($"Who should review the week of {Day(monday)} ({HoursText(total)} logged)? Eligible reviewers: {Names(reviewers)}. Ask the user to pick one, then call submit_timesheet again with approver.");

            
            if (said.Trim().ToLowerInvariant() is "me" or "myself" or "i" or "self")
                return Fail($"No one can review their own timesheet. Eligible reviewers: {Names(reviewers)}. Ask the user who should review it.");

            AssigneeCandidate chosen;
            if (long.TryParse(said, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric))
            {
                var byId = reviewers.FirstOrDefault(r => r.UserId == numeric);
                if (byId == null) return Fail($"User {numeric} is not an eligible reviewer. Eligible reviewers: {Names(reviewers)}.");
                chosen = byId;
            }
            else
            {
                var matches = PersonMatch.Find(said.Trim(), reviewers);
                if (matches.Count == 0) return Fail($"No eligible reviewer is called '{said}'. Eligible reviewers: {Names(reviewers)}. Ask the user who should review it.");
                if (matches.Count > 1) return Fail($"'{said}' fits more than one reviewer: {string.Join(", ", matches.Take(8).Select(m => $"{m.Name} (userId {m.UserId})"))}. Ask the user which one, then call submit_timesheet again with that person's userId.");
                chosen = matches[0];
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, JiraRoutes.SubmitTimesheet)
            {
                Content = JsonContent.Create(new { periodId, approverUserId = chosen.UserId })
            };
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
            using var response = await jira.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Fail($"The timesheet was not submitted: {ApiMessage(content, (int)response.StatusCode)}");

            JsonElement after = default;
            try { after = JsonDocument.Parse(content).RootElement; } catch (JsonException) { }

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                submitted = true,
                weekStart = Day(monday),
                weekEnd = Day(sunday),
                status = Text(after, "status") ?? "SUBMITTED",
                approver = chosen.Name ?? $"user {chosen.UserId}",
                loggedHours = Hours(LongProp(after, "totalMinutes") ?? total),
                weeklyTargetHours = WeeklyTargetHours,
                submittedAt = Text(after, "submittedAt")
            }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }
}

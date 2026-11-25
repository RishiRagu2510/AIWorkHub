using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;


public class GetTimesheetHistoryTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    private const int DailyTargetMinutes = 8 * 60;

    public override string Name => "get_timesheet_history";
    public override string Description => "Weekly timesheet history for the signed-in user: hours logged against the 8.0 hour daily target, the missing hours, and each week's status (DRAFT, SUBMITTED, APPROVED, REJECTED). Covers the last few weeks including this one.";
    public override string Category => "Chronos";
    public override string? RequiredPortal => "timesheet";
    public override string RequiredPermission => "TIMESHEET_VIEW";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["weeks"] = new { type = "INTEGER", description = "How many weeks to show, 1 to 12. Defaults to 4." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var weeks = Math.Clamp(Int(arguments, "weeks") ?? 4, 1, 12);
            var today = TodayLocal;
            var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var first = thisMonday.AddDays(-7 * (weeks - 1));
            var last = thisMonday.AddDays(6);

            using var report = await TryGetAsync(Jira(), $"{JiraRoutes.MyTimesheet}?From={first:yyyy-MM-dd}&To={last:yyyy-MM-dd}&PageSize=1", cancellationToken);
            if (report == null)
                return new ToolResult(false, null, "I could not read your timesheets right now.");

            var minutesByDay = new Dictionary<DateOnly, long>();
            if (report.RootElement.TryGetProperty("dailyTotals", out var totals) && totals.ValueKind == JsonValueKind.Array)
                foreach (var d in totals.EnumerateArray())
                    if (DateOnly.TryParse(StrOf(d, "date"), CultureInfo.InvariantCulture, out var date)) minutesByDay[date] = LongProp(d, "minutes") ?? 0;

            var periods = new Dictionary<DateOnly, JsonElement>();
            if (report.RootElement.TryGetProperty("periods", out var ps) && ps.ValueKind == JsonValueKind.Array)
                foreach (var p in ps.EnumerateArray())
                    if (DateOnly.TryParse(StrOf(p, "periodStart"), CultureInfo.InvariantCulture, out var start)) periods[start] = p.Clone();

            
            var holidays = new HashSet<DateOnly>();
            var identity = Identity();
            for (var year = first.Year; year <= last.Year; year++)
            {
                using var policy = await TryGetAsync(identity, $"{IdentityRoutes.AbsencePolicy}?year={year}", cancellationToken);
                if (policy != null && policy.RootElement.TryGetProperty("holidays", out var hs) && hs.ValueKind == JsonValueKind.Array)
                    foreach (var h in hs.EnumerateArray())
                        if (h.ValueKind == JsonValueKind.String && DateOnly.TryParse(h.GetString(), CultureInfo.InvariantCulture, out var hd)) holidays.Add(hd);
            }

            var rows = new List<object>();
            long sumLogged = 0, sumTarget = 0;
            for (var monday = first; monday <= thisMonday; monday = monday.AddDays(7))
            {
                long logged = 0, target = 0, weekend = 0;
                var days = new List<object>();
                for (var i = 0; i < 7; i++)
                {
                    var date = monday.AddDays(i);
                    var minutes = minutesByDay.GetValueOrDefault(date);
                    var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    if (isWeekend) { weekend += minutes; continue; }

                    var isHoliday = holidays.Contains(date);
                    var counts = !isHoliday && date <= today;
                    logged += minutes;
                    if (counts) target += DailyTargetMinutes;
                    days.Add(new
                    {
                        date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        weekday = date.DayOfWeek.ToString(),
                        loggedHours = Hours(minutes),
                        targetHours = counts ? Hours(DailyTargetMinutes) : 0,
                        missingHours = counts ? Hours(Math.Max(0, DailyTargetMinutes - minutes)) : 0,
                        note = isHoliday ? "Company holiday" : date > today ? "Upcoming" : null
                    });
                }

                periods.TryGetValue(monday, out var period);
                var hasPeriod = period.ValueKind == JsonValueKind.Object;
                var status = hasPeriod ? StrOf(period, "status") ?? "DRAFT" : logged + weekend > 0 ? "DRAFT" : "NOT_STARTED";
                var missing = Math.Max(0, target - logged);
                sumLogged += logged;
                sumTarget += target;

                rows.Add(new
                {
                    weekStart = monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    weekEnd = monday.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    status,
                    loggedHours = Hours(logged),
                    targetHours = Hours(target),
                    missingHours = Hours(missing),
                    weekendHours = weekend > 0 ? Hours(weekend) : (double?)null,
                    completionPercent = target == 0 ? (double?)null : Math.Round(logged * 100.0 / target, 0),
                    submittedAt = hasPeriod ? StrOf(period, "submittedAt") : null,
                    approvedAt = hasPeriod ? StrOf(period, "approvedAt") : null,
                    rejectionReason = hasPeriod ? StrOf(period, "rejectionReason") : null,
                    days
                });
            }

            rows.Reverse();
            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                dailyTargetHours = Hours(DailyTargetMinutes),
                assumption = "Targets count Monday to Friday up to today, excluding company holidays. Approved leave is not deducted.",
                weeks = rows,
                totals = new { loggedHours = Hours(sumLogged), targetHours = Hours(sumTarget), missingHours = Hours(Math.Max(0, sumTarget - sumLogged)) }
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}


public class ApproveTimesheetPeriodTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "approve_timesheet_period";
    public override string Description => "For team leads and project managers: list the weekly timesheets submitted for review (action 'list'), or approve or reject one (action 'approve' or 'reject' with its periodId). Only approve or reject after the user has said which one. Rejecting needs a reason the employee will see.";
    public override string Category => "Chronos";
    public override string? RequiredPortal => "timesheet";
    public override string RequiredPermission => "TIMESHEET_APPROVE";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["action"] = new { type = "STRING", description = "'list' (default), 'approve' or 'reject'." },
            ["periodId"] = new { type = "INTEGER", description = "The timesheet period to decide, from the list." },
            ["reason"] = new { type = "STRING", description = "Why it is rejected (required for reject). Optional for approve and then ignored." }
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

            var jira = Jira();
            var today = TodayLocal;
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var from = monday.AddDays(-7 * 12);
            var to = monday.AddDays(6);

            using var response = await jira.GetAsync($"{JiraRoutes.TeamTimesheets}?From={from:yyyy-MM-dd}&To={to:yyyy-MM-dd}&PageSize=1", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ToolResult(false, null, response.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "RBAC: reviewing team timesheets is limited to team leads, project managers and admins."
                    : $"I could not read the team's timesheets: {ApiMessage(body, (int)response.StatusCode)}");

            using var doc = JsonDocument.Parse(body);
            var callerId = CallerUserId(arguments);
            var submitted = new List<JsonElement>();
            if (doc.RootElement.TryGetProperty("periods", out var ps) && ps.ValueKind == JsonValueKind.Array)
                foreach (var p in ps.EnumerateArray())
                    if (StrOf(p, "status") == "SUBMITTED" && LongProp(p, "userId") != callerId) submitted.Add(p.Clone());

            var people = await EmployeeDirectory.LoadAsync(Identity(), cancellationToken);

            object Shape(JsonElement p)
            {
                var projects = new List<object>();
                if (p.TryGetProperty("projects", out var prj) && prj.ValueKind == JsonValueKind.Array)
                    foreach (var x in prj.EnumerateArray()) projects.Add(new { project = StrOf(x, "projectName"), hours = Hours(LongProp(x, "minutes") ?? 0) });
                var userId = LongProp(p, "userId");
                return new
                {
                    periodId = LongProp(p, "id"),
                    employee = EmployeeDirectory.NameOfUser(people, userId) ?? $"User {userId}",
                    weekStart = StrOf(p, "periodStart"),
                    weekEnd = StrOf(p, "periodEnd"),
                    loggedHours = Hours(LongProp(p, "totalMinutes") ?? 0),
                    billableHours = Hours(LongProp(p, "billableMinutes") ?? 0),
                    projects,
                    submittedAt = StrOf(p, "submittedAt")
                };
            }

            if (action == "list")
                return new ToolResult(true, JsonSerializer.Serialize(new { pendingCount = submitted.Count, periods = submitted.Select(Shape).ToArray() }));

            var periodId = Long(arguments, "periodId");
            if (periodId == null)
                return new ToolResult(false, null, "Which timesheet? Please give the period number from the list.");
            var reason = Str(arguments, "reason");
            if (action == "reject" && reason == null)
                return new ToolResult(false, null, "A reason is needed to reject a timesheet. What should I tell the employee?");

            var match = submitted.FirstOrDefault(p => LongProp(p, "id") == periodId);
            if (match.ValueKind != JsonValueKind.Object)
                return new ToolResult(false, null, $"Timesheet {periodId} is not waiting for your review, or it is your own.");

            var version = LongProp(match, "version");
            if (version == null) return new ToolResult(false, null, "That timesheet did not report a version, so I did not change it.");

            using var request = new HttpRequestMessage(HttpMethod.Post, JiraRoutes.TimesheetAction(action))
            {
                Content = JsonContent.Create(new { periodId, reason = action == "reject" ? reason : null })
            };
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
            using var decision = await jira.SendAsync(request, cancellationToken);
            var decisionBody = await decision.Content.ReadAsStringAsync(cancellationToken);
            if (!decision.IsSuccessStatusCode)
                return new ToolResult(false, null, $"The timesheet was not {(action == "approve" ? "approved" : "rejected")}: {ApiMessage(decisionBody, (int)decision.StatusCode)}");

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                decided = true,
                decision = action == "approve" ? "APPROVED" : "REJECTED",
                reason = action == "reject" ? reason : null,
                period = Shape(match)
            }));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}

using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AIAgent.Application.Tests;

public class NudgeServiceTests
{
    
    private static readonly DateOnly Wednesday = new(2026, 11, 11);
    private static readonly DateOnly Friday = new(2026, 11, 13);
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private sealed class FakeTool(string name, string permission, Func<Dictionary<string, object?>, ToolResult> run) : IAgentTool
    {
        public int Calls { get; private set; }
        public Dictionary<string, object?>? LastArguments { get; private set; }
        public string Name => name;
        public string Description => name;
        public string Category => "Test";
        public string RequiredPermission => permission;
        public object ParameterSchema => new { };

        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastArguments = arguments;
            return Task.FromResult(run(arguments));
        }
    }

    private sealed class Identity(VerifiedCaller? caller, bool credentials = true) : ICallerIdentityProvider
    {
        public bool HasCredentials => credentials;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default) => Task.FromResult(caller);
    }

    private static VerifiedCaller Caller(string role = "DEVELOPER", params string[] permissions) => new(7, role, [role], permissions, 1);

    private static FakeTool Ok(string name, string permission, string json) => new(name, permission, _ => new ToolResult(true, json));
    private static FakeTool Fails(string name, string permission) => new(name, permission, _ => new ToolResult(false, null, "nope"));

    private static NudgeService Service(VerifiedCaller? caller, DateOnly day, IMemoryCache? cache = null, params IAgentTool[] tools) =>
        new(tools, new Identity(caller), cache, () => day);

    private static async Task<List<Nudge>> Nudges(VerifiedCaller caller, DateOnly day, params IAgentTool[] tools) =>
        (await Service(caller, day, null, tools).GetAsync())!.Items.ToList();

    private const string Digest = """
        {"digest":true,"counts":{"overdue":2,"dueWithin7Days":3,"inProgress":1,"other":4,"total":10},
         "items":[{"issueKey":"FLP-9","summary":"Fix login","why":"overdue by 3 days, high priority"},
                  {"issueKey":"FLP-12","summary":"Update docs","why":"due in 2 days, medium priority"}]}
        """;

    private static string Week(string start, string status, double logged, double target, double missing, string? reason = null, string days = "[]") =>
        $$"""{"weekStart":"{{start}}","status":"{{status}}","loggedHours":{{logged}},"targetHours":{{target}},"missingHours":{{missing}},"rejectionReason":{{(reason is null ? "null" : $"\"{reason}\"")}},"days":{{days}}}""";

    private static string History(params string[] weeks) => "{\"weeks\":[" + string.Join(",", weeks) + "]}";

    

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NobodyUnconfirmed_GetsAnything(bool credentials)
    {
        var tool = Ok("get_work_digest", "ISSUE_VIEW", Digest);
        var service = new NudgeService([tool], new Identity(null, credentials), null, () => Wednesday);

        Assert.Null(await service.GetAsync());
        Assert.Equal(0, tool.Calls);
    }

    [Fact]
    public async Task ACheckIsSkippedWhenThePersonLacksItsPermission()
    {
        var digest = Ok("get_work_digest", "ISSUE_VIEW", Digest);
        var review = Ok("approve_timesheet_period", "TIMESHEET_APPROVE", "{\"pendingCount\":4,\"periods\":[]}");

        var nudges = await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Wednesday, digest, review);

        Assert.Empty(nudges);
        Assert.Equal(0, digest.Calls);
        Assert.Equal(0, review.Calls);
    }

    [Fact]
    public async Task AnAdminIsNotHeldToThePermissionList()
    {
        var review = Ok("approve_timesheet_period", "TIMESHEET_APPROVE", "{\"pendingCount\":2,\"periods\":[]}");

        var nudges = await Nudges(Caller("ADMIN"), Wednesday, review);

        Assert.Equal("timesheets-to-review", Assert.Single(nudges).Id);
    }

    [Fact]
    public async Task EveryCheckRunsAsTheCaller_WithTheReservedArgumentsSetByTheServer()
    {
        var digest = Ok("get_work_digest", "ISSUE_VIEW", Digest);

        await Nudges(Caller("DEVELOPER", "ISSUE_VIEW"), Wednesday, digest);

        Assert.Equal(7L, digest.LastArguments![IssueVisibility.CallerUserIdArgument]);
        Assert.Equal(7L, digest.LastArguments[IssueVisibility.OwnerArgument]);   
        Assert.Equal("DEVELOPER", digest.LastArguments[IssueVisibility.CallerRolesArgument]);
    }

    

    [Fact]
    public async Task OverdueAndDueSoonWork_IsNamed_AndOverdueComesFirst()
    {
        var nudges = await Nudges(Caller("DEVELOPER", "ISSUE_VIEW"), Wednesday, Ok("get_work_digest", "ISSUE_VIEW", Digest));

        Assert.Equal(["overdue", "due-soon"], nudges.Select(n => n.Id));
        Assert.Equal("2 overdue issues", nudges[0].Title);
        Assert.Equal(NudgeService.High, nudges[0].Severity);
        Assert.Contains("FLP-9 Fix login", nudges[0].Detail);
        Assert.Equal("3 issues due in the next 7 days", nudges[1].Title);
        Assert.Contains("FLP-12", nudges[1].Detail);
    }

    [Fact]
    public async Task ASingleOverdueIssue_IsNotPluralised()
    {
        var json = """{"counts":{"overdue":1,"dueWithin7Days":0},"items":[]}""";

        var nudge = Assert.Single(await Nudges(Caller("DEVELOPER", "ISSUE_VIEW"), Wednesday, Ok("get_work_digest", "ISSUE_VIEW", json)));

        Assert.Equal("1 overdue issue", nudge.Title);
    }

    [Fact]
    public async Task NothingOverdueOrDue_SaysNothing()
    {
        var json = """{"counts":{"overdue":0,"dueWithin7Days":0,"inProgress":2},"items":[]}""";

        Assert.Empty(await Nudges(Caller("DEVELOPER", "ISSUE_VIEW"), Wednesday, Ok("get_work_digest", "ISSUE_VIEW", json)));
    }

    

    [Fact]
    public async Task AnUnsubmittedLastWeek_IsFlagged_WithTheHoursStillMissing()
    {
        var history = History(Week("2026-11-09", "DRAFT", 8, 24, 16), Week("2026-11-02", "DRAFT", 32, 40, 8));

        var nudge = Assert.Single(await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Wednesday, Ok("get_timesheet_history", "TIMESHEET_VIEW", history)));

        Assert.Equal("timesheet-last-week", nudge.Id);
        Assert.Equal(NudgeService.High, nudge.Severity);
        Assert.Contains("8 h still missing", nudge.Detail);
    }

    [Fact]
    public async Task ARejectedLastWeek_ShowsTheReason()
    {
        var history = History(Week("2026-11-09", "DRAFT", 8, 24, 16), Week("2026-11-02", "REJECTED", 40, 40, 0, "Wrong project on Tuesday"));

        var nudge = Assert.Single(await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Wednesday, Ok("get_timesheet_history", "TIMESHEET_VIEW", history)));

        Assert.Equal("timesheet-rejected", nudge.Id);
        Assert.Contains("Wrong project on Tuesday", nudge.Detail);
    }

    [Theory]
    [InlineData("SUBMITTED")]
    [InlineData("APPROVED")]
    public async Task ALastWeekThatIsAlreadyInReview_IsNotFlagged(string status)
    {
        var history = History(Week("2026-11-09", "DRAFT", 8, 24, 16), Week("2026-11-02", status, 40, 40, 0));

        Assert.Empty(await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Wednesday, Ok("get_timesheet_history", "TIMESHEET_VIEW", history)));
    }

    [Fact]
    public async Task ALastWeekWithNoWorkingDays_IsNotFlagged()
    {
        var history = History(Week("2026-11-09", "DRAFT", 8, 24, 16), Week("2026-11-02", "NOT_STARTED", 0, 0, 0));   

        Assert.Empty(await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Wednesday, Ok("get_timesheet_history", "TIMESHEET_VIEW", history)));
    }

    [Theory]
    [InlineData(5, true)]    
    [InlineData(6, true)]    
    [InlineData(0, true)]    
    [InlineData(3, false)]   
    public async Task ThisWeeksDraft_IsOnlyOfferedFromFriday(int dayOfWeek, bool offered)
    {
        var day = Monday.AddDays(((dayOfWeek + 6) % 7));
        var history = History(Week("2026-11-09", "DRAFT", 24, 24, 0), Week("2026-11-02", "APPROVED", 40, 40, 0));

        var nudges = await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), day, Ok("get_timesheet_history", "TIMESHEET_VIEW", history));

        Assert.Equal(offered, nudges.Any(n => n.Id == "timesheet-this-week"));
    }

    [Fact]
    public async Task ThisWeeksDraft_WithNoHours_IsNotOffered()
    {
        var history = History(Week("2026-11-09", "DRAFT", 0, 24, 24), Week("2026-11-02", "APPROVED", 40, 40, 0));

        Assert.Empty(await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Friday, Ok("get_timesheet_history", "TIMESHEET_VIEW", history)));
    }

    [Fact]
    public async Task ANothingLoggedWorkingDay_YesterdayIsMentioned_ButNotAHolidayOrAWeekend()
    {
        string Days(string note) => $$"""[{"date":"2026-11-10","weekday":"Tuesday","loggedHours":0,"targetHours":8,"missingHours":8,"note":{{note}}}]""";
        FakeTool Tool(string note) => Ok("get_timesheet_history", "TIMESHEET_VIEW", History(Week("2026-11-09", "DRAFT", 0, 16, 16, days: Days(note)), Week("2026-11-02", "APPROVED", 40, 40, 0)));

        var forgot = await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Wednesday, Tool("null"));
        var holiday = await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), Wednesday, Tool("\"Company holiday\""));

        Assert.Equal("No time logged for Tuesday", Assert.Single(forgot).Title);
        Assert.Equal(NudgeService.Low, forgot[0].Severity);
        Assert.Empty(holiday);
        Assert.Empty(await Nudges(Caller("DEVELOPER", "TIMESHEET_VIEW"), new DateOnly(2026, 11, 15), Tool("null")));   
    }

    

    [Fact]
    public async Task ManagersAreToldWhatIsWaitingForThem()
    {
        var review = Ok("approve_timesheet_period", "TIMESHEET_APPROVE", "{\"pendingCount\":3,\"periods\":[]}");
        var leave = Ok("approve_leave_request", "EMPLOYEE_VIEW", """{"pendingCount":2,"requests":[{"employee":"Asha Kumar","startDate":"2026-11-30"},{"employee":"Ben Rao","startDate":"2026-11-16"}]}""");

        var nudges = await Nudges(Caller("MANAGER", "TIMESHEET_APPROVE", "EMPLOYEE_VIEW"), Wednesday, review, leave);

        Assert.Equal("3 timesheets waiting for your review", nudges.Single(n => n.Id == "timesheets-to-review").Title);
        var decide = nudges.Single(n => n.Id == "leave-to-decide");
        Assert.Equal("2 leave requests waiting for your decision", decide.Title);
        Assert.Contains("Nov 16", decide.Detail);   
        Assert.Contains("Ben Rao", decide.Detail);
        Assert.Equal(NudgeService.High, decide.Severity);   
    }

    [Fact]
    public async Task ALeaveRequestThatStartsLater_IsNotUrgent()
    {
        var leave = Ok("approve_leave_request", "EMPLOYEE_VIEW", """{"pendingCount":1,"requests":[{"employee":"Asha Kumar","startDate":"2026-12-20"}]}""");

        var nudge = Assert.Single(await Nudges(Caller("MANAGER", "EMPLOYEE_VIEW"), Wednesday, leave));

        Assert.Equal(NudgeService.Medium, nudge.Severity);
        Assert.Equal("1 leave request waiting for your decision", nudge.Title);
    }

    [Fact]
    public async Task NothingPending_SaysNothing()
    {
        var review = Ok("approve_timesheet_period", "TIMESHEET_APPROVE", "{\"pendingCount\":0,\"periods\":[]}");
        var leave = Ok("approve_leave_request", "EMPLOYEE_VIEW", "{\"pendingCount\":0,\"requests\":[]}");

        Assert.Empty(await Nudges(Caller("MANAGER", "TIMESHEET_APPROVE", "EMPLOYEE_VIEW"), Wednesday, review, leave));
    }

    

    [Fact]
    public async Task ACheckThatFails_IsLeftOut_WithoutLosingTheOthers()
    {
        var broken = Fails("get_work_digest", "ISSUE_VIEW");
        var throws = new FakeTool("get_timesheet_history", "TIMESHEET_VIEW", _ => throw new InvalidOperationException("service down"));
        var garbage = Ok("approve_leave_request", "EMPLOYEE_VIEW", "not json at all");
        var review = Ok("approve_timesheet_period", "TIMESHEET_APPROVE", "{\"pendingCount\":1,\"periods\":[]}");

        var nudges = await Nudges(Caller("MANAGER", "ISSUE_VIEW", "TIMESHEET_VIEW", "EMPLOYEE_VIEW", "TIMESHEET_APPROVE"), Wednesday, broken, throws, garbage, review);

        Assert.Equal("timesheets-to-review", Assert.Single(nudges).Id);
    }

    [Fact]
    public async Task TheMostUrgentComeFirst()
    {
        var digest = Ok("get_work_digest", "ISSUE_VIEW", Digest);                                           
        var review = Ok("approve_timesheet_period", "TIMESHEET_APPROVE", "{\"pendingCount\":1,\"periods\":[]}");   
        var history = Ok("get_timesheet_history", "TIMESHEET_VIEW", History(Week("2026-11-09", "DRAFT", 8, 24, 16), Week("2026-11-02", "DRAFT", 32, 40, 8)));   

        var nudges = await Nudges(Caller("MANAGER", "ISSUE_VIEW", "TIMESHEET_VIEW", "TIMESHEET_APPROVE"), Wednesday, review, history, digest);

        Assert.Equal(["high", "high", "medium", "medium"], nudges.Select(n => n.Severity));
        Assert.Equal("overdue", nudges[0].Id);   
    }

    [Fact]
    public async Task EveryNudgeHasAPromptThePersonCanSend()
    {
        var digest = Ok("get_work_digest", "ISSUE_VIEW", Digest);
        var review = Ok("approve_timesheet_period", "TIMESHEET_APPROVE", "{\"pendingCount\":1,\"periods\":[]}");
        var leave = Ok("approve_leave_request", "EMPLOYEE_VIEW", "{\"pendingCount\":1,\"requests\":[]}");
        var history = Ok("get_timesheet_history", "TIMESHEET_VIEW", History(Week("2026-11-09", "DRAFT", 8, 24, 16), Week("2026-11-02", "DRAFT", 32, 40, 8)));

        var nudges = await Nudges(Caller("MANAGER", "ISSUE_VIEW", "TIMESHEET_VIEW", "TIMESHEET_APPROVE", "EMPLOYEE_VIEW"), Wednesday, digest, review, leave, history);

        Assert.True(nudges.Count >= 5);
        Assert.All(nudges, n => { Assert.False(string.IsNullOrWhiteSpace(n.Prompt)); Assert.False(string.IsNullOrWhiteSpace(n.Title)); });
        Assert.Equal(nudges.Count, nudges.Select(n => n.Id).Distinct().Count());
    }

    

    [Fact]
    public async Task TheAnswerIsKeptBriefly_PerPerson()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var digest = Ok("get_work_digest", "ISSUE_VIEW", Digest);
        var asha = Service(Caller("DEVELOPER", "ISSUE_VIEW"), Wednesday, cache, digest);

        await asha.GetAsync();
        await asha.GetAsync();
        Assert.Equal(1, digest.Calls);   

        var ben = new NudgeService([digest], new Identity(new VerifiedCaller(8, "DEVELOPER", ["DEVELOPER"], ["ISSUE_VIEW"], 1)), cache, () => Wednesday);
        await ben.GetAsync();
        Assert.Equal(2, digest.Calls);   
    }
}

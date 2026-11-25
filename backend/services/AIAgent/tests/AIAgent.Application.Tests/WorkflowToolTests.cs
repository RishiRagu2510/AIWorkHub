using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;


internal sealed class Routes
{
    private readonly List<(string Method, string Prefix, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = [];

    public Routes Get(string prefix, string json, HttpStatusCode status = HttpStatusCode.OK) => On("GET", prefix, json, status);
    public Routes Post(string prefix, string json, HttpStatusCode status = HttpStatusCode.OK) => On("POST", prefix, json, status);
    public Routes Put(string prefix, string json, HttpStatusCode status = HttpStatusCode.OK) => On("PUT", prefix, json, status);

    private Routes On(string method, string prefix, string json, HttpStatusCode status)
    {
        _routes.Add((method, prefix, _ => Http.Json(json, status)));
        return this;
    }

    public HttpResponseMessage Handle(HttpRequestMessage request)
    {
        var path = request.RequestUri!.PathAndQuery;
        foreach (var route in _routes)
            if (request.Method.Method == route.Method && path.StartsWith(route.Prefix, StringComparison.OrdinalIgnoreCase))
                return route.Respond(request);
        return Http.Json("{\"message\":\"not found\"}", HttpStatusCode.NotFound);
    }

    public (RecordingHandler Handler, FakeHttpClientFactory Factory) Build()
    {
        var handler = new RecordingHandler(Handle);
        return (handler, new FakeHttpClientFactory(handler));
    }
}

public class WorkflowToolTests
{
    private static Dictionary<string, object?> Args(string json, string? roles = null, long? caller = null, long? owner = null)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        if (roles != null) args[IssueVisibility.CallerRolesArgument] = roles;
        if (caller.HasValue) args[IssueVisibility.CallerUserIdArgument] = caller.Value;
        if (owner.HasValue) args[IssueVisibility.OwnerArgument] = owner.Value;
        return args;
    }

    private static JsonDocument Data(ToolResult result)
    {
        Assert.True(result.Success, result.Error);
        return JsonDocument.Parse((string)result.Data!);
    }

    private static HttpContextAccessor Anyone => new();
    private static string InDays(int days) => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(days).ToString("yyyy-MM-dd");

    private static readonly string People = "{\"items\":[{\"id\":1,\"userId\":11,\"firstName\":\"Asha\",\"lastName\":\"Kumar\",\"employeeCode\":\"E1\",\"designationId\":100,\"employmentStatus\":\"ACTIVE\"}," +
                                            "{\"id\":2,\"userId\":12,\"firstName\":\"Ben\",\"lastName\":\"Rao\",\"employeeCode\":\"E2\",\"designationId\":100,\"managerEmployeeId\":1,\"employmentStatus\":\"ACTIVE\"}],\"totalPages\":1}";

    

    [Fact]
    public async Task CreateIssue_Asks_AssigneeThenPointsThenDate_BeforeCallingAnything()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new CreateIssueTool(factory, Anyone).ExecuteAsync(Args("{\"projectId\":1,\"summary\":\"Fix login\"}"));

        Assert.False(result.Success);
        Assert.Contains("assignee", result.Error);
        Assert.Contains("story points", result.Error);
        Assert.Contains("due date", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(21)]
    public async Task CreateIssue_RejectsStoryPointsOutsideFibonacci(int points)
    {
        var (handler, factory) = new Routes().Build();

        var result = await new CreateIssueTool(factory, Anyone).ExecuteAsync(
            Args($"{{\"projectId\":1,\"summary\":\"x\",\"unassigned\":true,\"noDueDate\":true,\"storyPoints\":{points}}}"));

        Assert.False(result.Success);
        Assert.Contains("1, 2, 3, 5, 8 or 13", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task CreateIssue_RejectsADueDateInThePast()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new CreateIssueTool(factory, Anyone).ExecuteAsync(
            Args($"{{\"projectId\":1,\"summary\":\"x\",\"unassigned\":true,\"storyPoints\":3,\"dueDate\":\"{InDays(-2)}\"}}"));

        Assert.False(result.Success);
        Assert.Contains("in the past", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task CreateIssue_RefusesAnAssigneeWhoIsNotOnTheProject()
    {
        var (handler, factory) = new Routes().Get("/api/projects/1/assignment-members", "[{\"userId\":2}]").Build();

        var result = await new CreateIssueTool(factory, Anyone).ExecuteAsync(
            Args($"{{\"projectId\":1,\"summary\":\"x\",\"assigneeUserId\":9,\"storyPoints\":3,\"dueDate\":\"{InDays(3)}\"}}"));

        Assert.False(result.Success);
        Assert.Contains("not a member", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task CreateIssue_WithEverythingGiven_SendsItAllAndReturnsAFlatCard()
    {
        var due = InDays(7);
        var (handler, factory) = new Routes()
            .Get("/api/projects/1/assignment-members", "[{\"userId\":11}]")
            .Post("/api/issues", "{\"id\":77,\"issueKey\":\"FLP360-77\",\"summary\":\"Fix login\",\"statusCode\":\"TODO\",\"priority\":\"HIGH\",\"storyPoints\":5,\"assigneeUserId\":11,\"assigneeUserIds\":[11],\"version\":1}", HttpStatusCode.Created)
            .Get("/api/employees?", People)
            .Build();

        var result = await new CreateIssueTool(factory, Anyone).ExecuteAsync(
            Args($"{{\"projectId\":1,\"summary\":\"Fix login\",\"issueType\":\"bug\",\"priority\":\"high\",\"assigneeUserId\":11,\"storyPoints\":5,\"dueDate\":\"{due}\"}}"));

        using var body = JsonDocument.Parse(handler.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body!);
        Assert.Equal(1, body.RootElement.GetProperty("projectId").GetInt64());
        Assert.Equal("BUG", body.RootElement.GetProperty("issueType").GetString());
        Assert.Equal("HIGH", body.RootElement.GetProperty("priority").GetString());
        Assert.Equal(5, body.RootElement.GetProperty("storyPoints").GetInt32());
        Assert.Equal(due, body.RootElement.GetProperty("dueDate").GetString());
        Assert.Equal(11, body.RootElement.GetProperty("assigneeUserId").GetInt64());
        Assert.Equal(11, body.RootElement.GetProperty("assigneeUserIds")[0].GetInt64());

        using var data = Data(result);
        Assert.True(data.RootElement.GetProperty("created").GetBoolean());
        Assert.Equal("FLP360-77", data.RootElement.GetProperty("issueKey").GetString());
        Assert.Equal("Fix login", data.RootElement.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task CreateIssue_AllowsExplicitlySkippingEachOptionalDetail()
    {
        var (handler, factory) = new Routes().Post("/api/issues", "{\"id\":1,\"issueKey\":\"A-1\",\"summary\":\"x\"}", HttpStatusCode.Created).Build();

        var result = await new CreateIssueTool(factory, Anyone).ExecuteAsync(
            Args("{\"projectId\":1,\"summary\":\"x\",\"unassigned\":true,\"noStoryPoints\":true,\"noDueDate\":true}"));

        Assert.True(result.Success, result.Error);
        using var body = JsonDocument.Parse(handler.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body!);
        Assert.False(body.RootElement.TryGetProperty("assigneeUserId", out _));
        Assert.False(body.RootElement.TryGetProperty("storyPoints", out _));
        Assert.False(body.RootElement.TryGetProperty("dueDate", out _));
    }

    [Fact]
    public async Task CreateIssue_ForANewEmployee_AddsTheOnboardingChecklistAndLabel()
    {
        var (handler, factory) = new Routes().Post("/api/issues", "{\"id\":2,\"issueKey\":\"HR-2\",\"summary\":\"Onboard Priya\"}", HttpStatusCode.Created).Build();

        var result = await new CreateIssueTool(factory, Anyone).ExecuteAsync(
            Args("{\"projectId\":4,\"summary\":\"Onboard Priya\",\"template\":\"EMPLOYEE_ONBOARDING\"}"));

        Assert.True(result.Success, result.Error);
        using var body = JsonDocument.Parse(handler.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body!);
        Assert.Contains("- [ ]", body.RootElement.GetProperty("description").GetString());
        Assert.Contains("onboarding", body.RootElement.GetProperty("labels").EnumerateArray().Select(l => l.GetString()));
    }

    

    [Fact]
    public async Task ListProjects_ForAManager_ListsEveryProjectNumbered()
    {
        var (handler, factory) = new Routes().Get("/api/projects", "[{\"id\":5,\"key\":\"VISA\",\"name\":\"VISA\"},{\"id\":1,\"key\":\"FLP\",\"displayName\":\"FLP360\"}]").Build();

        var result = await new ListProjectsTool(factory, Anyone).ExecuteAsync(Args("{}"));

        using var data = Data(result);
        var projects = data.RootElement.GetProperty("projects");
        Assert.Equal("/api/projects", handler.Calls.Single().Request.RequestUri!.AbsolutePath);
        Assert.Equal(1, projects[0].GetProperty("number").GetInt32());
        Assert.Equal("FLP360", projects[0].GetProperty("name").GetString());
        Assert.Equal(2, projects[1].GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task ListProjects_ForADeveloper_ListsOnlyTheirOwnProjects()
    {
        var (handler, factory) = new Routes().Get("/api/projects/by-user/7", "[{\"id\":3,\"key\":\"AZ\",\"projectName\":\"Azure Migration\"}]").Build();

        var result = await new ListProjectsTool(factory, Anyone).ExecuteAsync(Args("{}", owner: 7));

        using var data = Data(result);
        Assert.Equal("/api/projects/by-user/7", handler.Calls.Single().Request.RequestUri!.AbsolutePath);
        Assert.Equal("Azure Migration", data.RootElement.GetProperty("projects")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListProjectMembers_ShowsNamesNotJustIds()
    {
        var (_, factory) = new Routes()
            .Get("/api/projects/1/assignment-members", "[{\"userId\":12},{\"userId\":11}]")
            .Get("/api/employees?", People)
            .Build();

        var result = await new ListProjectMembersTool(factory, Anyone).ExecuteAsync(Args("{\"projectId\":1}"));

        using var data = Data(result);
        var members = data.RootElement.GetProperty("members");
        Assert.Equal("Asha Kumar", members[0].GetProperty("name").GetString());
        Assert.Equal(11, members[0].GetProperty("userId").GetInt64());
        Assert.Equal("Ben Rao", members[1].GetProperty("name").GetString());
    }

    

    private const string IssueJson = "{\"id\":55,\"issueKey\":\"PAYCARD-1\",\"issueNumber\":1,\"summary\":\"Card declined\",\"issueType\":\"BUG\",\"statusCode\":\"IN_PROGRESS\",\"priority\":\"HIGH\"," +
                                     "\"projectId\":2,\"assigneeUserId\":11,\"assigneeUserIds\":[11],\"reporterUserId\":12,\"dueDate\":\"2000-01-01\",\"originalEstimateMinutes\":480,\"loggedTimeMinutes\":150,\"remainingEstimateMinutes\":330," +
                                     "\"description\":\"Cards fail at checkout\",\"version\":3}";

    [Fact]
    public async Task IssueDetails_ResolvesShorthandLikeP2Dash1_AndReturnsStructuredTelemetry()
    {
        var (_, factory) = new Routes()
            .Get("/api/issues/key/", "{}", HttpStatusCode.NotFound)
            .Get("/api/issues/search?Page=1&PageSize=100&Text=", "{\"items\":[]}")
            .Get("/api/projects", "[{\"id\":2,\"key\":\"PAYCARD\",\"displayName\":\"P2 PayCard\"},{\"id\":3,\"key\":\"AZ\",\"name\":\"Azure\"}]")
            .Get("/api/issues/search?ProjectId=2", "{\"items\":[{\"id\":55,\"issueNumber\":1,\"issueKey\":\"PAYCARD-1\"}]}")
            .Get("/api/issues/55", IssueJson)
            .Get("/api/employees?", People)
            .Build();

        var result = await new GetIssueDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"issueKey\":\"P2-1\"}"));

        using var data = Data(result);
        var root = data.RootElement;
        Assert.Equal("PAYCARD-1", root.GetProperty("issueKey").GetString());
        Assert.Equal("IN_PROGRESS", root.GetProperty("statusCode").GetString());
        Assert.Equal(2.5, root.GetProperty("time").GetProperty("loggedHours").GetDouble());
        Assert.Equal(5.5, root.GetProperty("time").GetProperty("remainingHours").GetDouble());
        Assert.Equal("Asha Kumar", root.GetProperty("assignees")[0].GetProperty("name").GetString());
        Assert.Contains("overdue", root.GetProperty("dueStatus").GetString());
    }

    [Fact]
    public async Task IssueDetails_WhenTwoProjectsMatchTheShorthand_DoesNotGuess()
    {
        var (_, factory) = new Routes()
            .Get("/api/issues/key/", "{}", HttpStatusCode.NotFound)
            .Get("/api/issues/search?Page=1&PageSize=100&Text=", "{\"items\":[]}")
            .Get("/api/projects", "[{\"id\":2,\"key\":\"P2A\",\"name\":\"A\"},{\"id\":3,\"key\":\"P2B\",\"name\":\"B\"}]")
            .Get("/api/issues/search?ProjectId=", "{\"items\":[{\"id\":55,\"issueNumber\":1},{\"id\":56,\"issueNumber\":9}]}")
            .Build();

        var result = await new GetIssueDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"issueKey\":\"P2-1\"}"));

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    

    [Theory]
    [InlineData("in review", "CODE_REVIEW")]
    [InlineData("QA", "TESTING")]
    [InlineData("In Progress", "IN_PROGRESS")]
    [InlineData("done", "DONE")]
    [InlineData("to do", "TODO")]
    public async Task Transition_UnderstandsEverydayStatusNames(string typed, string expected)
    {
        var (handler, factory) = new Routes()
            .Get("/api/issues/55", "{\"id\":55,\"issueKey\":\"X-1\",\"summary\":\"s\",\"statusCode\":\"BACKLOG_OTHER\",\"version\":3}")
            .Put("/api/issues/55/transition", "{\"id\":55,\"statusCode\":\"" + expected + "\",\"version\":4}")
            .Build();

        var result = await new TransitionIssueTool(factory, Anyone).ExecuteAsync(Args($"{{\"issueIdOrKey\":\"55\",\"statusCode\":\"{typed}\"}}"));

        Assert.True(result.Success, result.Error);
        var put = handler.Calls.Single(c => c.Request.Method == HttpMethod.Put);
        using var body = JsonDocument.Parse(put.Body!);
        Assert.Equal(expected, body.RootElement.GetProperty("statusCode").GetString());
    }

    [Fact]
    public async Task Transition_SendsTheFetchedVersionInIfMatch_AndReturnsAFlatCard()
    {
        var (handler, factory) = new Routes()
            .Get("/api/issues/55", "{\"id\":55,\"issueKey\":\"FLP360-17\",\"summary\":\"Fix login\",\"statusCode\":\"TODO\",\"version\":3}")
            .Put("/api/issues/55/transition", "{\"id\":55,\"statusCode\":\"IN_PROGRESS\",\"version\":4}")
            .Build();

        var result = await new TransitionIssueTool(factory, Anyone).ExecuteAsync(Args("{\"issueIdOrKey\":\"55\",\"statusCode\":\"IN_PROGRESS\"}"));

        var put = handler.Calls.Single(c => c.Request.Method == HttpMethod.Put);
        Assert.Equal("\"3\"", put.Request.Headers.IfMatch.Single().ToString());
        using var data = Data(result);
        Assert.Equal("FLP360-17", data.RootElement.GetProperty("issueKey").GetString());
        Assert.Equal("Fix login", data.RootElement.GetProperty("summary").GetString());
        Assert.Equal("TODO", data.RootElement.GetProperty("previousStatus").GetString());
        Assert.Equal("IN_PROGRESS", data.RootElement.GetProperty("statusCode").GetString());
        Assert.Equal(4, data.RootElement.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Transition_WhenTheIssueIsAlreadyThere_SaysSoWithoutCallingTheApi()
    {
        var (handler, factory) = new Routes().Get("/api/issues/55", "{\"id\":55,\"issueKey\":\"X-1\",\"statusCode\":\"DONE\",\"version\":3}").Build();

        var result = await new TransitionIssueTool(factory, Anyone).ExecuteAsync(Args("{\"issueIdOrKey\":\"55\",\"statusCode\":\"done\"}"));

        Assert.False(result.Success);
        Assert.Contains("already DONE", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task Transition_WithoutAVersion_DoesNotGuessOne()
    {
        var (handler, factory) = new Routes().Get("/api/issues/55", "{\"id\":55,\"issueKey\":\"X-1\",\"statusCode\":\"TODO\"}").Build();

        var result = await new TransitionIssueTool(factory, Anyone).ExecuteAsync(Args("{\"issueIdOrKey\":\"55\",\"statusCode\":\"DONE\"}"));

        Assert.False(result.Success);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Put);
    }

    

    [Fact]
    public async Task LogWork_WithAStartTime_RecordsStartAndEnd_AndReturnsACompactReceipt()
    {
        var (handler, factory) = new Routes()
            .Post("/api/worklogs", "{\"id\":9,\"issueKey\":\"FLP360-17\",\"issueSummary\":\"Fix login\",\"workDate\":\"2026-11-05\",\"durationMinutes\":150,\"description\":\"Fixed bug\",\"periodStatus\":\"DRAFT\",\"startTime\":\"14:30:00\",\"endTime\":\"17:00:00\"}", HttpStatusCode.Created)
            .Build();

        var result = await new LogWorkTool(factory, Anyone).ExecuteAsync(
            Args("{\"issueIdOrKey\":\"17\",\"durationMinutes\":150,\"workDate\":\"2026-11-05\",\"description\":\"Fixed bug\",\"startTime\":\"14:30\"}"));

        using var body = JsonDocument.Parse(handler.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body!);
        Assert.Equal("14:30:00", body.RootElement.GetProperty("startTime").GetString());
        Assert.Equal("17:00:00", body.RootElement.GetProperty("endTime").GetString());
        Assert.Equal("Fixed bug", body.RootElement.GetProperty("description").GetString());
        using var data = Data(result);
        Assert.True(data.RootElement.GetProperty("logged").GetBoolean());
        Assert.Equal(2.5, data.RootElement.GetProperty("hours").GetDouble());
        Assert.Equal("FLP360-17", data.RootElement.GetProperty("issueKey").GetString());
    }

    [Fact]
    public async Task LogWork_WithoutAStartTime_NeverInventsOne()
    {
        var (handler, factory) = new Routes().Post("/api/worklogs", "{\"id\":9,\"durationMinutes\":60}", HttpStatusCode.Created).Build();

        await new LogWorkTool(factory, Anyone).ExecuteAsync(Args("{\"issueIdOrKey\":\"17\",\"durationMinutes\":60}"));

        using var body = JsonDocument.Parse(handler.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body!);
        Assert.False(body.RootElement.TryGetProperty("startTime", out _));
        Assert.False(body.RootElement.TryGetProperty("endTime", out _));
    }

    [Fact]
    public async Task LogWork_RejectsAnUnreadableStartTime()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new LogWorkTool(factory, Anyone).ExecuteAsync(Args("{\"issueIdOrKey\":\"17\",\"durationMinutes\":60,\"startTime\":\"after lunch\"}"));

        Assert.False(result.Success);
        Assert.Contains("HH:mm", result.Error);
        Assert.Empty(handler.Calls);
    }

    

    private static Routes LeaveService(long sickAvailable, bool hasReports = false) => new Routes()
        .Get("/api/employees/absence/policy", "{\"configured\":true,\"holidays\":[],\"version\":1}")
        .Get("/api/employees/absence?", "{\"items\":[],\"totalPages\":1,\"hasReports\":" + (hasReports ? "true" : "false") + ",\"policyConfigured\":true,\"manager\":{\"id\":1,\"name\":\"Asha Kumar\"}," +
                                        "\"balances\":[{\"kind\":\"CASUAL\",\"allowance\":12,\"used\":2,\"pending\":0,\"available\":10},{\"kind\":\"SICK\",\"allowance\":12,\"used\":4,\"pending\":1,\"available\":" + sickAvailable + "},{\"kind\":\"EARNED\",\"allowance\":12,\"used\":0,\"pending\":0,\"available\":12}]}");

    [Fact]
    public async Task CheckLeaves_ShowsLabelledBalances_AndHowToApply()
    {
        var (_, factory) = LeaveService(7).Build();

        var result = await new CheckEmployeeLeavesTool(factory, Anyone).ExecuteAsync(Args("{\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-02\"}"));

        using var data = Data(result);
        var sick = data.RootElement.GetProperty("balances").EnumerateArray().Single(b => b.GetProperty("kind").GetString() == "SICK");
        Assert.Equal("Sick leave", sick.GetProperty("label").GetString());
        Assert.Equal(12, sick.GetProperty("allocated").GetInt64());
        Assert.Equal(4, sick.GetProperty("used").GetInt64());
        Assert.Equal(7, sick.GetProperty("available").GetInt64());
        Assert.Contains(data.RootElement.GetProperty("balances").EnumerateArray(), b => b.GetProperty("label").GetString() == "Earned leave");
        Assert.DoesNotContain(data.RootElement.GetProperty("balances").EnumerateArray(), b => b.GetProperty("label").GetString()!.Contains("Annual PTO"));
        Assert.Contains("type", data.RootElement.GetProperty("howToApply").GetString());
    }

    private const string TeamRows = "{\"items\":[" +
        "{\"id\":9,\"employeeName\":\"Vetri Selvam Pari\",\"kind\":\"WFH\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-05\",\"days\":5,\"status\":\"PENDING\",\"reason\":\"private\"}," +
        "{\"id\":10,\"employeeName\":\"Ben Rao\",\"kind\":\"SICK\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-01\",\"days\":1,\"status\":\"APPROVED\"}],\"totalPages\":1}";

    [Fact]
    public async Task CheckLeaves_ForADirectReport_ReturnsTheRequestNumber_AndSaysItCanBeDecided()
    {
        var (handler, factory) = new Routes().Get("/api/employees/absence?year", TeamRows).Build();

        var result = await new CheckEmployeeLeavesTool(factory, Anyone).ExecuteAsync(Args("{\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\",\"scope\":\"team\",\"employee\":\"vetri selvam\"}"));

        using var data = Data(result);
        var item = data.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Contains("team=true", handler.Calls.First().Request.RequestUri!.Query);
        Assert.Equal(9, item.GetProperty("requestId").GetInt64());
        Assert.True(item.GetProperty("canDecide").GetBoolean());
        Assert.DoesNotContain("private", (string)result.Data!);
    }

    [Fact]
    public async Task CheckLeaves_OnlyPendingRequestsOfDirectReports_CanBeDecided()
    {
        var (_, factory) = new Routes().Get("/api/employees/absence?year", TeamRows).Build();
        var tool = new CheckEmployeeLeavesTool(factory, Anyone);

        using var team = Data(await tool.ExecuteAsync(Args("{\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\",\"scope\":\"team\"}")));
        using var everyone = Data(await tool.ExecuteAsync(Args("{\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\",\"scope\":\"all\"}")));
        using var mine = Data(await tool.ExecuteAsync(Args("{\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\"}")));

        Assert.Equal([true, false], team.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("canDecide").GetBoolean()).ToArray());
        Assert.Equal([9L], everyone.RootElement.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("canDecide").GetBoolean()).Select(i => i.GetProperty("requestId").GetInt64()).ToArray());
        Assert.All(mine.RootElement.GetProperty("items").EnumerateArray(), i => Assert.False(i.GetProperty("canDecide").GetBoolean()));
    }

    [Fact]
    public async Task CheckLeaves_HrCanDecideForAnyone_ButNotForTheirOwnRequest()
    {
        const string everyone = "{\"items\":[" +
            "{\"id\":9,\"employeeName\":\"Rishi Ragupathi\",\"kind\":\"WFH\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-05\",\"days\":5,\"status\":\"PENDING\"}," +
            "{\"id\":11,\"employeeName\":\"Hema HR\",\"kind\":\"SICK\",\"startDate\":\"2027-03-02\",\"endDate\":\"2027-03-02\",\"days\":1,\"status\":\"PENDING\"}],\"totalPages\":1}";
        var (_, factory) = new Routes()
            .Get("/api/employees/absence?year=2027&team=true", "{\"items\":[],\"totalPages\":1}")
            .Get("/api/employees/absence?year=2027&all=true", everyone)
            .Get("/api/employees/absence?year=2027&page", "{\"items\":[{\"id\":11,\"employeeName\":\"Hema HR\",\"kind\":\"SICK\",\"startDate\":\"2027-03-02\",\"endDate\":\"2027-03-02\",\"status\":\"PENDING\"}],\"totalPages\":1}")
            .Build();
        var tool = new CheckEmployeeLeavesTool(factory, Anyone);
        var range = "\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\",\"scope\":\"all\"";

        using var hr = Data(await tool.ExecuteAsync(Args("{" + range + "}", roles: "HR")));
        using var developer = Data(await tool.ExecuteAsync(Args("{" + range + "}", roles: "DEVELOPER")));

        Assert.Equal([9L], hr.RootElement.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("canDecide").GetBoolean()).Select(i => i.GetProperty("requestId").GetInt64()).ToArray());
        Assert.All(developer.RootElement.GetProperty("items").EnumerateArray(), i => Assert.False(i.GetProperty("canDecide").GetBoolean()));
    }

    [Fact]
    public async Task ApproveLeave_Hr_ListsEveryonesPendingRequests_ExceptTheirOwn()
    {
        var year = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).Year;
        const string everyone = "{\"items\":[" +
            "{\"id\":9,\"employeeName\":\"Rishi Ragupathi\",\"kind\":\"WFH\",\"startDate\":\"2026-10-29\",\"endDate\":\"2026-11-30\",\"days\":23,\"status\":\"PENDING\",\"version\":3}," +
            "{\"id\":11,\"employeeName\":\"Hema HR\",\"kind\":\"SICK\",\"startDate\":\"2026-11-02\",\"endDate\":\"2026-11-02\",\"days\":1,\"status\":\"PENDING\",\"version\":1}],\"totalPages\":1}";
        var (handler, factory) = new Routes()
            .Post("/api/employees/absence/9/decision", "{\"id\":9,\"status\":\"APPROVED\"}")
            .Get($"/api/employees/absence?year={year}&all=true", everyone)
            .Get($"/api/employees/absence?year={year + 1}&all=true", "{\"items\":[],\"totalPages\":1}")
            .Get("/api/employees/absence?year", "{\"items\":[{\"id\":11,\"status\":\"PENDING\"}],\"totalPages\":1}")
            .Build();
        var tool = new ApproveLeaveRequestTool(factory, Anyone);

        using var listed = Data(await tool.ExecuteAsync(Args("{}", roles: "HR")));
        var own = await tool.ExecuteAsync(Args("{\"action\":\"approve\",\"requestId\":11}", roles: "HR"));
        var decided = await tool.ExecuteAsync(Args("{\"action\":\"approve\",\"requestId\":9}", roles: "HR"));

        Assert.Equal(1, listed.RootElement.GetProperty("pendingCount").GetInt32());
        Assert.Equal("Rishi Ragupathi", listed.RootElement.GetProperty("requests")[0].GetProperty("employee").GetString());
        Assert.False(own.Success);
        Assert.True(decided.Success, decided.Error);
        Assert.Single(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task CheckLeaves_ADeveloper_CannotReadEveryonesLeave()
    {
        const string rows = "{\"items\":[{\"id\":9,\"employeeName\":\"Rishi Ragupathi\",\"kind\":\"WFH\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-05\",\"days\":5,\"status\":\"PENDING\"}],\"totalPages\":1}";
        var (handler, factory) = new Routes()
            .Get("/api/employees/absence?year=2027&team=true", "{\"items\":[],\"totalPages\":1}")
            .Get("/api/employees/absence?year=2027&all=true", rows)
            .Build();
        var tool = new CheckEmployeeLeavesTool(factory, Anyone);

        using var everyone = Data(await tool.ExecuteAsync(Args("{\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\",\"scope\":\"all\"}", roles: "DEVELOPER")));
        using var named = Data(await tool.ExecuteAsync(Args("{\"employee\":\"Rishi Ragupathi\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\"}", roles: "DEVELOPER")));

        Assert.Equal(0, everyone.RootElement.GetProperty("count").GetInt32());
        Assert.Contains("Only HR and Admin", everyone.RootElement.GetProperty("note").GetString());
        Assert.True(named.RootElement.GetProperty("restricted").GetBoolean());
        Assert.DoesNotContain("Rishi", named.RootElement.GetProperty("items").ToString());
        Assert.DoesNotContain(handler.Calls, c => c.Request.RequestUri!.Query.Contains("all=true"));
    }

    [Fact]
    public async Task CheckLeaves_ADirectReportsManager_StillSeesTheTeam_ButHrAndAdminSeeEveryone()
    {
        const string rows = "{\"items\":[{\"id\":9,\"employeeName\":\"Rishi Ragupathi\",\"kind\":\"WFH\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-05\",\"days\":5,\"status\":\"APPROVED\"}],\"totalPages\":1}";
        var (handler, factory) = new Routes()
            .Get("/api/employees/absence?year=2027&team=true", rows)
            .Get("/api/employees/absence?year=2027&all=true", rows)
            .Get("/api/employees/absence?year=2027&page", "{\"items\":[],\"totalPages\":1}")
            .Build();
        var tool = new CheckEmployeeLeavesTool(factory, Anyone);
        var range = "\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-31\"";

        using var manager = Data(await tool.ExecuteAsync(Args("{" + range + ",\"scope\":\"team\",\"employee\":\"Rishi\"}", roles: "PROJECT_MANAGER")));
        using var admin = Data(await tool.ExecuteAsync(Args("{" + range + ",\"scope\":\"all\",\"employee\":\"Rishi\"}", roles: "ADMIN")));

        Assert.Equal(1, manager.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(1, admin.RootElement.GetProperty("count").GetInt32());
        Assert.Contains(handler.Calls, c => c.Request.RequestUri!.Query.Contains("all=true"));
    }

    [Fact]
    public async Task ApplyLeave_OverTheQuota_IsRefusedWithTheExactMessage_AndNothingIsSubmitted()
    {
        var (handler, factory) = LeaveService(7).Build();

        
        var result = await new ApplyLeaveTool(factory, Anyone).ExecuteAsync(Args("{\"leaveType\":\"SICK\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-10\"}"));

        Assert.False(result.Success);
        Assert.Equal("Request exceeds remaining quota: 8 days requested, but only 7 days available. Would you like to apply for 7 days instead or use unpaid leave?", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task ApplyLeave_WithinTheQuota_Submits_AndMapsAnnualPtoToEarned()
    {
        var (handler, factory) = LeaveService(7).Post("/api/employees/absence", "{\"id\":31,\"kind\":\"EARNED\",\"days\":3,\"status\":\"PENDING\"}").Build();

        var result = await new ApplyLeaveTool(factory, Anyone).ExecuteAsync(Args("{\"leaveType\":\"Annual PTO\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-03\",\"reason\":\"Family trip\"}"));

        using var body = JsonDocument.Parse(handler.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body!);
        Assert.Equal("EARNED", body.RootElement.GetProperty("kind").GetString());
        Assert.Equal("2027-03-01", body.RootElement.GetProperty("startDate").GetString());
        Assert.Equal("Family trip", body.RootElement.GetProperty("reason").GetString());
        using var data = Data(result);
        Assert.True(data.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal(9, data.RootElement.GetProperty("remainingAfter").GetInt64());
        Assert.Equal("Asha Kumar", data.RootElement.GetProperty("awaitingApprovalFrom").GetString());
    }

    [Fact]
    public async Task PrepareLeave_OffersExactlyTheFiveTypes_WithDaysLeft_AndSubmitsNothing()
    {
        var (handler, factory) = LeaveService(7).Build();

        var result = await new PrepareLeaveTool(factory, Anyone).ExecuteAsync(Args("{}"));

        Assert.True(result.Success, result.Error);
        using var data = Data(result);
        var types = data.RootElement.GetProperty("types").EnumerateArray().ToList();
        Assert.Equal(new[] { "Floating holidays", "Sick leave", "Earned leave", "Unpaid leave", "Work from home" }, types.Select(t => t.GetProperty("label").GetString()).ToArray());
        Assert.Equal(new[] { "CASUAL", "SICK", "EARNED", "UNPAID", "WFH" }, types.Select(t => t.GetProperty("kind").GetString()).ToArray());
        Assert.Equal(7, types[1].GetProperty("available").GetInt64());
        Assert.Equal(12, types[1].GetProperty("allowance").GetInt64());
        Assert.Equal(4, types[1].GetProperty("used").GetInt64());
        Assert.Equal(1, types[1].GetProperty("pending").GetInt64());
        Assert.Equal(JsonValueKind.Null, types[3].GetProperty("available").ValueKind);
        Assert.Equal("Asha Kumar", data.RootElement.GetProperty("manager").GetString());
        Assert.True(data.RootElement.GetProperty("policyConfigured").GetBoolean());
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("floating holidays", "CASUAL")]
    [InlineData("Work from home", "WFH")]
    [InlineData("sick", "SICK")]
    public async Task PrepareLeave_PreselectsATypeTheUserNamed(string named, string kind)
    {
        var (_, factory) = LeaveService(7).Build();

        var result = await new PrepareLeaveTool(factory, Anyone).ExecuteAsync(Args("{\"leaveType\":\"" + named + "\"}"));

        using var data = Data(result);
        Assert.Equal(kind, data.RootElement.GetProperty("selectedKind").GetString());
    }

    [Fact]
    public async Task PrepareLeave_SaysSoWhenTheEmployeeServiceCannotBeReached()
    {
        var (_, factory) = new Routes().Build();

        var result = await new PrepareLeaveTool(factory, Anyone).ExecuteAsync(Args("{}"));

        Assert.False(result.Success);
        Assert.Contains("could not", result.Error);
    }

    [Fact]
    public async Task ApplyLeave_Unpaid_HasNoQuotaToExceed()
    {
        var (handler, factory) = LeaveService(0).Post("/api/employees/absence", "{\"id\":32,\"days\":10,\"status\":\"PENDING\"}").Build();

        var result = await new ApplyLeaveTool(factory, Anyone).ExecuteAsync(Args("{\"leaveType\":\"unpaid\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-12\"}"));

        Assert.True(result.Success, result.Error);
        Assert.Single(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task ApplyLeave_ThatHasNoWorkingDays_IsRefused()
    {
        var (handler, factory) = LeaveService(7).Build();

        
        var result = await new ApplyLeaveTool(factory, Anyone).ExecuteAsync(Args("{\"leaveType\":\"SICK\",\"startDate\":\"2027-03-06\",\"endDate\":\"2027-03-07\"}"));

        Assert.False(result.Success);
        Assert.Contains("no working days", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("{\"leaveType\":\"holiday-ish\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-01\"}", "which type")]
    [InlineData("{\"leaveType\":\"SICK\",\"startDate\":\"tomorrow\",\"endDate\":\"2027-03-01\"}", "YYYY-MM-DD")]
    [InlineData("{\"leaveType\":\"SICK\",\"startDate\":\"2027-03-05\",\"endDate\":\"2027-03-01\"}", "before")]
    [InlineData("{\"leaveType\":\"SICK\",\"startDate\":\"2027-12-30\",\"endDate\":\"2028-01-04\"}", "two calendar years")]
    public async Task ApplyLeave_RefusesUnclearRequests_WithoutCallingAnything(string json, string expected)
    {
        var (handler, factory) = new Routes().Build();

        var result = await new ApplyLeaveTool(factory, Anyone).ExecuteAsync(Args(json));

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Calls);
    }

    private const string PendingTeam = "{\"items\":[{\"id\":9,\"employeeName\":\"Ben Rao\",\"employeeCode\":\"E2\",\"kind\":\"CASUAL\",\"startDate\":\"2027-03-01\",\"endDate\":\"2027-03-02\",\"days\":2,\"reason\":\"Wedding\",\"status\":\"PENDING\",\"version\":2}," +
                                       "{\"id\":10,\"employeeName\":\"Old Request\",\"kind\":\"SICK\",\"status\":\"APPROVED\",\"version\":1}],\"totalPages\":1,\"hasReports\":true}";

    [Fact]
    public async Task ApproveLeave_List_ShowsOnlyPendingRequestsOfDirectReports()
    {
        var (_, factory) = new Routes().Get("/api/employees/absence?year", PendingTeam).Build();

        var result = await new ApproveLeaveRequestTool(factory, Anyone).ExecuteAsync(Args("{}"));

        using var data = Data(result);
        Assert.Equal(1, data.RootElement.GetProperty("pendingCount").GetInt32());
        Assert.Equal("Ben Rao", data.RootElement.GetProperty("requests")[0].GetProperty("employee").GetString());
    }

    [Fact]
    public async Task ApproveLeave_Approve_SendsTheDecisionWithTheRequestVersion()
    {
        var (handler, factory) = new Routes().Get("/api/employees/absence?year", PendingTeam).Post("/api/employees/absence/9/decision", "{\"id\":9,\"status\":\"APPROVED\"}").Build();

        var result = await new ApproveLeaveRequestTool(factory, Anyone).ExecuteAsync(Args("{\"action\":\"approve\",\"requestId\":9}"));

        using var body = JsonDocument.Parse(handler.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body!);
        Assert.Equal("APPROVE", body.RootElement.GetProperty("action").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("version").GetInt64());
        using var data = Data(result);
        Assert.Equal("APPROVED", data.RootElement.GetProperty("decision").GetString());
    }

    [Fact]
    public async Task ApproveLeave_RejectNeedsAReason_AndAnUnknownRequestIsRefused()
    {
        var (handler, factory) = new Routes().Get("/api/employees/absence?year", PendingTeam).Build();
        var tool = new ApproveLeaveRequestTool(factory, Anyone);

        var noReason = await tool.ExecuteAsync(Args("{\"action\":\"reject\",\"requestId\":9}"));
        var unknown = await tool.ExecuteAsync(Args("{\"action\":\"approve\",\"requestId\":999}"));

        Assert.False(noReason.Success);
        Assert.Contains("reason", noReason.Error);
        Assert.False(unknown.Success);
        Assert.Contains("999", unknown.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task ApproveLeave_ForSomeoneWithoutReports_ExplainsWhy()
    {
        var (_, factory) = new Routes().Get("/api/employees/absence?year", "{\"items\":[],\"totalPages\":1,\"hasReports\":false}").Build();

        var result = await new ApproveLeaveRequestTool(factory, Anyone).ExecuteAsync(Args("{}"));

        Assert.False(result.Success);
        Assert.Contains("no direct reports", result.Error);
    }

    

    private const string Career =
        "[{\"id\":2,\"effectiveDate\":\"2026-08-17\",\"recordedAt\":\"2026-10-15T09:00:00Z\",\"changeType\":\"CAREER_CHANGE\",\"note\":null,\"details\":{\"grade\":\"T1\",\"designation\":\"Junior\",\"jobTitle\":\"Junior Software Engineer\",\"department\":\"Developer\"}}," +
        "{\"id\":1,\"effectiveDate\":\"2026-01-19\",\"recordedAt\":\"2026-10-08T09:00:00Z\",\"changeType\":\"BASELINE\",\"note\":\"Joined as intern\",\"details\":{\"grade\":\"T0\",\"designation\":\"Intern\",\"jobTitle\":\"Intern Software Engineer\",\"department\":\"Developer\"}}]";

    [Fact]
    public async Task CareerHistory_ListsEachPeriodNewestFirst_LikeThePersonaTable()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/me", "{\"id\":5,\"userId\":15,\"employeeCode\":\"EMP-005\",\"firstName\":\"Rishi\",\"lastName\":\"Ragupathi\"}")
            .Get("/api/employees/5/career-history", Career)
            .Build();

        var result = await new GetCareerHistoryTool(factory, Anyone).ExecuteAsync(Args("{}"));

        using var data = Data(result);
        var periods = data.RootElement.GetProperty("periods");
        Assert.Equal(2, data.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("Rishi Ragupathi", data.RootElement.GetProperty("employee").GetString());

        Assert.Equal("2026-08-17 → Present", periods[0].GetProperty("period").GetString());
        Assert.Equal("Current position", periods[0].GetProperty("status").GetString());
        Assert.Equal("T1 · Junior", periods[0].GetProperty("gradeDesignation").GetString());
        Assert.Equal("Junior Software Engineer", periods[0].GetProperty("jobTitle").GetString());
        Assert.Equal("—", periods[0].GetProperty("notes").GetString());

        Assert.Equal("2026-01-19 → 2026-08-17", periods[1].GetProperty("period").GetString());
        Assert.Equal("Previous recorded details", periods[1].GetProperty("status").GetString());
        Assert.Equal("0 year(s), 6 month(s), 29 day(s)", periods[1].GetProperty("periodLength").GetString());
        Assert.Equal("Joined as intern", periods[1].GetProperty("notes").GetString());
        Assert.Equal("2026-10-08", periods[1].GetProperty("recorded").GetString());
    }

    [Fact]
    public async Task CareerHistory_ForSomeoneWhoLeft_EndsTheLastPeriodAtTheirLastDay_NotPresent()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/5/career-history", Career)
            .Get("/api/employees/5", "{\"id\":5,\"firstName\":\"A\",\"lastName\":\"B\",\"endDate\":\"2026-09-30\"}")
            .Build();

        var result = await new GetCareerHistoryTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":5}"));

        using var data = Data(result);
        var latest = data.RootElement.GetProperty("periods")[0];
        Assert.Equal("2026-08-17 → 2026-09-30", latest.GetProperty("period").GetString());
        Assert.Equal("Previous period", latest.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CareerHistory_WithNothingRecorded_SaysSo()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/me", "{\"id\":5,\"firstName\":\"A\",\"lastName\":\"B\"}")
            .Get("/api/employees/5/career-history", "[]")
            .Build();

        var result = await new GetCareerHistoryTool(factory, Anyone).ExecuteAsync(Args("{}"));

        using var data = Data(result);
        Assert.Equal(0, data.RootElement.GetProperty("count").GetInt32());
        Assert.Contains("No career changes recorded", data.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task CareerHistory_WhenNotAllowedToRead_SaysSoInsteadOfShowingNothing()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/me", "{\"id\":5,\"firstName\":\"A\",\"lastName\":\"B\"}")
            .Get("/api/employees/5/career-history", "{}", HttpStatusCode.Forbidden)
            .Build();

        var result = await new GetCareerHistoryTool(factory, Anyone).ExecuteAsync(Args("{}"));

        Assert.False(result.Success);
        Assert.Contains("could not be read", result.Error);
    }

    

    private static Routes MeAs5() => new Routes()
        .Get("/api/employees/me", "{\"id\":5,\"userId\":15,\"employeeCode\":\"EMP-005\",\"firstName\":\"Rishi\",\"lastName\":\"Ragupathi\",\"preferredName\":null,\"workEmail\":\"rishi@x.com\",\"phone\":\"999\",\"locationId\":9}");

    [Fact]
    public async Task EmployeeRecord_Personal_ShowsTheSameFieldsAsThePersonalTab()
    {
        var (_, factory) = MeAs5().Get("/api/employees/references", "[{\"id\":9,\"type\":\"LOCATION\",\"name\":\"Chennai\"}]").Build();

        var result = await new GetEmployeeRecordTool(factory, Anyone).ExecuteAsync(Args("{\"section\":\"personal\"}", caller: 15));

        using var data = Data(result);
        var items = data.RootElement.GetProperty("items");
        Assert.Equal(6, items.GetArrayLength());
        Assert.Equal("First name", items[0].GetProperty("field").GetString());
        Assert.Equal("Rishi", items[0].GetProperty("value").GetString());
        Assert.Equal("—", items[2].GetProperty("value").GetString());
        Assert.Equal("rishi@x.com", items[3].GetProperty("value").GetString());
        Assert.Equal("Chennai", items[5].GetProperty("value").GetString());
    }

    [Fact]
    public async Task EmployeeRecord_Experience_ListsEarlierJobsWithPeriods()
    {
        var (_, factory) = MeAs5()
            .Get("/api/employees/5/experience", "[{\"company\":\"Acme\",\"title\":\"Dev\",\"description\":null,\"startDate\":\"2024-01-01\",\"endDate\":\"2025-06-30\"},{\"company\":\"Beta\",\"title\":\"Lead\",\"description\":\"APIs\",\"startDate\":\"2025-07-01\",\"endDate\":null}]")
            .Build();

        var result = await new GetEmployeeRecordTool(factory, Anyone).ExecuteAsync(Args("{\"section\":\"experience\"}", caller: 15));

        using var data = Data(result);
        var items = data.RootElement.GetProperty("items");
        Assert.Equal(2, data.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("2024-01-01 → 2025-06-30", items[0].GetProperty("period").GetString());
        Assert.Equal("—", items[0].GetProperty("description").GetString());
        Assert.Equal("2025-07-01 → Present", items[1].GetProperty("period").GetString());
    }

    [Theory]
    [InlineData("education", "[{\"institution\":\"MIT\",\"qualification\":\"BSc\",\"fieldOfStudy\":\"CS\",\"startDate\":\"2018-08-01\",\"endDate\":\"2022-05-01\"}]", "institution", "MIT")]
    [InlineData("certifications", "[{\"name\":\"AZ-900\",\"issuer\":\"Microsoft\",\"issuedDate\":\"2025-01-01\",\"expiryDate\":null,\"credentialId\":\"C1\",\"credentialUrl\":null,\"fileUrl\":\"/secret/file\"}]", "name", "AZ-900")]
    [InlineData("languages", "[{\"name\":\"English\",\"proficiency\":\"Fluent\"}]", "language", "English")]
    public async Task EmployeeRecord_OtherTabs_ReturnTheirRows(string section, string json, string property, string expected)
    {
        var (_, factory) = MeAs5().Get($"/api/employees/5/{section}", json).Build();

        var result = await new GetEmployeeRecordTool(factory, Anyone).ExecuteAsync(Args($"{{\"section\":\"{section}\"}}", caller: 15));

        using var data = Data(result);
        Assert.Equal(expected, data.RootElement.GetProperty("items")[0].GetProperty(property).GetString());
        Assert.DoesNotContain("secret", data.RootElement.GetRawText());   
    }

    [Fact]
    public async Task EmployeeRecord_WithNothingRecorded_SaysSo_AndRefusesAnUnknownSection()
    {
        var (_, factory) = MeAs5().Get("/api/employees/5/languages", "[]").Build();
        var tool = new GetEmployeeRecordTool(factory, Anyone);

        using var empty = Data(await tool.ExecuteAsync(Args("{\"section\":\"languages\"}", caller: 15)));
        Assert.Contains("No languages recorded", empty.RootElement.GetProperty("message").GetString());

        var bad = await tool.ExecuteAsync(Args("{\"section\":\"salary\"}", caller: 15));
        Assert.False(bad.Success);
        Assert.Contains("Section must be one of", bad.Error);
    }

    [Fact]
    public async Task EmployeeRecord_WhenNotAllowedToRead_SaysSo()
    {
        var (_, factory) = MeAs5().Get("/api/employees/5/education", "{}", HttpStatusCode.Forbidden).Build();

        var result = await new GetEmployeeRecordTool(factory, Anyone).ExecuteAsync(Args("{\"section\":\"education\"}", caller: 15));

        Assert.False(result.Success);
        Assert.Contains("could not be read", result.Error);
    }

    

    [Fact]
    public async Task PrepareSkills_ListsWhatTheyHave_AndOnlyTheSkillsTheyCanStillAdd()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/me/skills", "[{\"id\":7,\"skillId\":1,\"skillName\":\"React\",\"category\":\"Frontend\",\"proficiency\":4,\"yearsOfExperience\":2.5,\"lastUsedDate\":\"2026-10-01\"}]")
            .Get("/api/employees/me", "{\"id\":5,\"userId\":15,\"version\":42}")
            .Get("/api/employees/career-catalog", "{\"skills\":[{\"id\":1,\"name\":\"React\",\"category\":\"Frontend\"},{\"id\":2,\"name\":\"Kubernetes\",\"category\":\"Cloud\"},{\"id\":3,\"name\":\"SQL\",\"category\":null}]}")
            .Build();

        var result = await new PrepareSkillsTool(factory, Anyone).ExecuteAsync(Args("{}", caller: 15));

        using var data = Data(result);
        Assert.Equal("skills", data.RootElement.GetProperty("form").GetString());
        Assert.Equal(42, data.RootElement.GetProperty("version").GetInt64());

        var mine = data.RootElement.GetProperty("skills");
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(7, mine[0].GetProperty("id").GetInt64());
        Assert.Equal("React", mine[0].GetProperty("skill").GetString());
        Assert.Equal(4, mine[0].GetProperty("proficiency").GetInt64());
        Assert.Equal(2.5m, mine[0].GetProperty("yearsOfExperience").GetDecimal());

        var available = data.RootElement.GetProperty("available").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new[] { "Kubernetes", "SQL" }, available);   
    }

    [Fact]
    public async Task PrepareSkills_WhenTheProfileCannotBeLoaded_SaysSo()
    {
        var (_, factory) = new Routes().Get("/api/employees/me", "{}", HttpStatusCode.ServiceUnavailable).Build();

        var result = await new PrepareSkillsTool(factory, Anyone).ExecuteAsync(Args("{}", caller: 15));

        Assert.False(result.Success);
        Assert.Contains("could not load your profile", result.Error);
    }

    

    [Fact]
    public async Task PrepareDelete_OpensTheFormWithoutCallingAnyService()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new PrepareDeleteTool(factory, Anyone).ExecuteAsync(Args("{\"kind\":\"Skills\",\"issue\":\"flp-1\"}", caller: 15));

        using var data = Data(result);
        Assert.Equal("delete", data.RootElement.GetProperty("form").GetString());
        Assert.Equal("skills", data.RootElement.GetProperty("kind").GetString());
        Assert.Equal("FLP-1", data.RootElement.GetProperty("issueKey").GetString());
        Assert.Empty(handler.Calls);   
    }

    [Fact]
    public async Task PrepareDelete_DropsAKindItDoesNotKnow()
    {
        var (_, factory) = new Routes().Build();

        var result = await new PrepareDeleteTool(factory, Anyone).ExecuteAsync(Args("{\"kind\":\"projects\"}", caller: 15));

        using var data = Data(result);
        Assert.Equal("", data.RootElement.GetProperty("kind").GetString());
    }

    

    private static Routes EveryTab(Routes? first = null) => (first ?? new Routes())
        .Get("/api/employees/1/skills", "[{\"skillName\":\"React\",\"category\":\"Frontend\",\"proficiency\":4,\"yearsOfExperience\":2,\"lastUsedDate\":null}]")
        .Get("/api/employees/1/career-history", "[{\"id\":5,\"effectiveDate\":\"2024-01-01\",\"changeType\":\"PROMOTION\",\"details\":{\"jobTitle\":\"Engineer\",\"department\":\"Tech\"}}]")
        .Get("/api/employees/1/experience", "[{\"company\":\"Initech\",\"title\":\"Dev\",\"startDate\":\"2019-01-01\",\"endDate\":\"2021-01-01\"}]")
        .Get("/api/employees/1/education", "[{\"institution\":\"MIT\",\"qualification\":\"BSc\",\"fieldOfStudy\":\"CS\",\"startDate\":\"2015-08-01\",\"endDate\":\"2019-05-01\"}]")
        .Get("/api/employees/1/certifications", "[{\"name\":\"AWS SAA\",\"issuer\":\"Amazon\"}]")
        .Get("/api/employees/1/languages", "[{\"name\":\"French\",\"proficiency\":\"Basic\"}]")
        .Get("/api/employees/1/compensation", "[{\"ctc\":1200000,\"bonus\":100000,\"currency\":\"INR\",\"effectiveFrom\":\"2020-01-01\",\"effectiveTo\":null}]")
        .Get("/api/employees/1", "{\"id\":1,\"userId\":11,\"firstName\":\"Asha\",\"lastName\":\"Kumar\",\"employeeCode\":\"E1\",\"workEmail\":\"asha@x.com\",\"phone\":\"999\"}")
        .Get("/api/employees/references", "[]")
        .Get("/api/employees?", People);

    [Fact]
    public async Task FullDetails_GivesHrEveryTabInOneCall()
    {
        var (_, factory) = EveryTab().Build();

        var result = await new GetEmployeeFullDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":1}", roles: "HR", caller: 99));

        using var data = Data(result);
        var root = data.RootElement;
        Assert.Equal("Asha Kumar", root.GetProperty("employee").GetProperty("name").GetString());
        Assert.Equal("React", root.GetProperty("employee").GetProperty("skills")[0].GetProperty("skill").GetString());
        Assert.Equal(1, root.GetProperty("careerHistory").GetProperty("periods").GetArrayLength());
        Assert.Equal("Initech", root.GetProperty("experience").GetProperty("items")[0].GetProperty("company").GetString());
        Assert.Equal("MIT", root.GetProperty("education").GetProperty("items")[0].GetProperty("institution").GetString());
        Assert.Equal("AWS SAA", root.GetProperty("certifications").GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal("French", root.GetProperty("languages").GetProperty("items")[0].GetProperty("language").GetString());
        Assert.Equal("asha@x.com", root.GetProperty("personal").GetProperty("items").EnumerateArray().First(i => i.GetProperty("field").GetString() == "Work email").GetProperty("value").GetString());
        Assert.Equal(1200000, root.GetProperty("compensation").GetProperty("current").GetProperty("ctc").GetDecimal());
        Assert.Equal(0, root.GetProperty("unavailable").GetArrayLength());
    }

    [Fact]
    public async Task FullDetails_NamesAPartThatCouldNotBeReadInsteadOfCallingItEmpty()
    {
        var (_, factory) = EveryTab(new Routes().Get("/api/employees/1/languages", "{}", HttpStatusCode.Forbidden)).Build();

        var result = await new GetEmployeeFullDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":1}", roles: "ADMIN", caller: 99));

        using var data = Data(result);
        var unavailable = data.RootElement.GetProperty("unavailable").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Single(unavailable);
        Assert.StartsWith("languages", unavailable[0]);
        Assert.Equal("MIT", data.RootElement.GetProperty("education").GetProperty("items")[0].GetProperty("institution").GetString());
    }

    [Theory]
    [InlineData("DEVELOPER")]
    [InlineData("MANAGER")]
    public async Task FullDetails_IsRefusedForEveryoneButHrAndAdmin(string role)
    {
        var (handler, factory) = EveryTab().Build();

        var result = await new GetEmployeeFullDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":1}", roles: role, caller: 99));

        Assert.False(result.Success);
        Assert.Contains("RBAC", result.Error);
        Assert.Empty(handler.Calls);   
    }

    [Fact]
    public async Task FullDetails_AsksWhichPersonWhenTheNameIsAmbiguous()
    {
        var (_, factory) = EveryTab().Build();

        var result = await new GetEmployeeFullDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"query\":\"a\"}", roles: "HR", caller: 99));

        using var data = Data(result);
        Assert.True(data.RootElement.GetProperty("needsChoice").GetBoolean());
    }

    

    private static Routes Everyone() => new Routes()
        .Get("/api/employees/1/skills", "[{\"skillName\":\"React\",\"proficiency\":4,\"yearsOfExperience\":2}]")
        .Get("/api/employees/1/experience", "[{\"company\":\"Initech\",\"title\":\"Dev\",\"startDate\":\"2019-01-01\",\"endDate\":\"2021-01-01\"}]")
        .Get("/api/employees/1/education", "[{\"institution\":\"MIT\",\"qualification\":\"BSc\",\"fieldOfStudy\":\"CS\"}]")
        .Get("/api/employees/1/certifications", "[{\"name\":\"AWS SAA\",\"issuer\":\"Amazon\"}]")
        .Get("/api/employees/1/languages", "[{\"name\":\"French\",\"proficiency\":\"Basic\"}]")
        .Get("/api/employees/1/compensation", "[{\"ctc\":1200000,\"bonus\":100000,\"currency\":\"INR\",\"effectiveFrom\":\"2020-01-01\",\"effectiveTo\":null}]")
        .Get("/api/employees/2/skills", "[]")
        .Get("/api/employees/2/experience", "[]")
        .Get("/api/employees/2/education", "[]")
        .Get("/api/employees/2/certifications", "[]")
        .Get("/api/employees/2/languages", "[]")
        .Get("/api/employees/2/compensation", "{}", HttpStatusCode.Forbidden)
        .Get("/api/employees/1/career-history", "[{\"id\":3,\"effectiveDate\":\"2023-01-01\",\"details\":{\"jobTitle\":\"Junior Engineer\",\"designation\":\"Junior\",\"grade\":\"T1\",\"department\":\"Developer\"}},{\"id\":9,\"effectiveDate\":\"2025-01-01\",\"details\":{\"jobTitle\":\"Software Engineer\",\"designation\":\"Associate\",\"grade\":\"T2\",\"department\":\"Developer\"}}]")
        .Get("/api/employees/2/career-history", "[]")
        .Get("/api/employees/1", "{\"id\":1,\"phone\":\"+91 98765 43210\"}")
        .Get("/api/employees/2", "{\"id\":2}")
        .Get("/api/employees/references", "[{\"id\":100,\"kind\":\"DESIGNATION\",\"name\":\"Engineer\"}]")
        .Get("/api/employees?", People);

    [Fact]
    public async Task AllEmployees_GivesHrEveryoneWithTheirDetailsAndAFile()
    {
        var (_, factory) = Everyone().Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "HR", caller: 99));

        using var data = Data(result);
        var root = data.RootElement;
        Assert.Equal(2, root.GetProperty("count").GetInt32());

        var asha = root.GetProperty("employees")[0];
        Assert.Equal("Asha Kumar", asha.GetProperty("name").GetString());
        Assert.Equal("E1", asha.GetProperty("employeeCode").GetString());
        Assert.Equal("Engineer", asha.GetProperty("designation").GetString());
        Assert.Contains("React (Advanced, 2 yrs)", asha.GetProperty("skills").GetString());
        Assert.Equal(1, asha.GetProperty("counts").GetProperty("certifications").GetInt32());
        Assert.Equal("1200000 INR", asha.GetProperty("currentCtc").GetString());

        var ben = root.GetProperty("employees")[1];
        Assert.Equal("Asha Kumar", ben.GetProperty("manager").GetString());
        Assert.Equal("compensation", ben.GetProperty("partsNotRead").GetString());   

        var file = root.GetProperty("file");
        Assert.Matches(@"^All_Employees_Report_\d{4}-\d{2}-\d{2}\.xlsx$", file.GetProperty("fileName").GetString());
        Assert.Equal("table", file.GetProperty("format").GetString());

        using var sheet = JsonDocument.Parse(file.GetProperty("content").GetString()!);
        var columns = sheet.RootElement.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("header").GetString()).ToList();
        var table = sheet.RootElement.GetProperty("rows");
        Assert.Equal(2, table.GetArrayLength());
        Assert.Equal("Employee ID", columns[0]);
        Assert.Equal("#E1", table[0][0].GetString());
        Assert.Equal("+91 98765 43210", table[0][columns.IndexOf("Phone")].GetString());       
        Assert.Equal("Software Engineer", table[0][columns.IndexOf("Job Title")].GetString()); 
        Assert.Equal("Engineer", table[0][columns.IndexOf("Designation")].GetString());          
        Assert.Equal("T2", table[0][columns.IndexOf("Grade")].GetString());
        Assert.Equal(JsonValueKind.Null, table[1][columns.IndexOf("Job Title")].ValueKind);
        var types = sheet.RootElement.GetProperty("columns").EnumerateArray().ToDictionary(c => c.GetProperty("header").GetString()!, c => c.GetProperty("type").GetString());
        Assert.Equal("status", types["Status"]);
        Assert.Equal("tag", types["Department"]);
        Assert.Equal("number", types["Current CTC"]);
        Assert.All(sheet.RootElement.GetProperty("columns").EnumerateArray(), c => Assert.False(string.IsNullOrEmpty(c.GetProperty("group").GetString())));
        Assert.Contains("Department", sheet.RootElement.GetProperty("breakdowns").EnumerateArray().Select(b => b.GetString()));
        Assert.Contains("Initech", table[0][columns.IndexOf("Experience")].GetString());
        Assert.Equal("• AWS SAA - Amazon", table[0][columns.IndexOf("Certifications")].GetString());   
        Assert.Equal("list", types["Skills"]);
        Assert.Equal(1200000, table[0][columns.IndexOf("Current CTC")].GetDecimal());   
        Assert.Equal(JsonValueKind.Null, table[1][columns.IndexOf("Current CTC")].ValueKind);
        Assert.Equal("2", sheet.RootElement.GetProperty("summary")[0].GetProperty("value").GetString());
        Assert.Equal("Total Employees", sheet.RootElement.GetProperty("summary")[0].GetProperty("label").GetString());
    }

    [Fact]
    public async Task AllEmployees_KeepsTheFileContentAwayFromTheModel()
    {
        var (_, factory) = Everyone().Build();
        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "ADMIN", caller: 99));

        var forModel = System.Text.Json.JsonSerializer.Serialize(AIAgent.Application.Services.ToolPayload.ForModel(AIAgent.Application.Services.ToolPayload.Unpack(result.Data)));

        Assert.DoesNotContain("Initech", forModel);   
        Assert.Contains("contentWithheldFromAssistant", forModel);
    }

    [Fact]
    public async Task AllEmployees_PassesTheStatusFilterOn()
    {
        var (handler, factory) = Everyone().Build();

        await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"status\":\"active\"}", roles: "HR", caller: 99));

        Assert.Contains(handler.Calls, c => c.Request.RequestUri!.Query.Contains("EmploymentStatus=ACTIVE"));
    }

    [Theory]
    [InlineData("HUMAN_RESOURCE")]   
    [InlineData("HR")]
    [InlineData("HR_ADMIN")]
    [InlineData("ADMIN")]
    [InlineData("human_resource")]
    public async Task AllEmployees_IsAllowedForEveryNameHrGoesBy(string role)
    {
        var (_, factory) = Everyone().Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: role, caller: 99));

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task FullDetails_IsAllowedForTheHumanResourceRole()
    {
        var (_, factory) = EveryTab().Build();

        var result = await new GetEmployeeFullDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"query\":\"Asha\"}", roles: "HUMAN_RESOURCE", caller: 99));

        Assert.DoesNotContain("RBAC", result.Error ?? "");
    }

    [Theory]
    [InlineData("TALENT_ACQUISITION")]
    [InlineData("ENGINEERING_MANAGER")]
    [InlineData("HUMAN")]
    public async Task AllEmployees_StaysRefusedForRolesThatOnlyLookSimilar(string role)
    {
        var (handler, factory) = Everyone().Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: role, caller: 99));

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData("DEVELOPER")]
    [InlineData("MANAGER")]
    public async Task AllEmployees_IsRefusedForEveryoneButHrAndAdmin(string role)
    {
        var (handler, factory) = Everyone().Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: role, caller: 99));

        Assert.False(result.Success);
        Assert.Contains("RBAC", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task AllEmployees_SaysSoWhenTheDirectoryCannotBeRead()
    {
        var (_, factory) = new Routes().Get("/api/employees?", "{}", HttpStatusCode.Forbidden).Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "HR", caller: 99));

        Assert.False(result.Success);
        Assert.Contains("could not be read", result.Error);
    }

    

    private static readonly string Batch =
        "{\"items\":[" +
        "{\"id\":1,\"employeeCode\":\"E1\",\"firstName\":\"Asha\",\"lastName\":\"Kumar\",\"phone\":\"+91 98765 43210\",\"designationId\":100,\"employmentStatus\":\"ACTIVE\"," +
        "\"career\":{\"jobTitle\":\"Software Engineer\",\"designation\":\"Associate\",\"grade\":\"T2\",\"department\":\"Developer\"}," +
        "\"skills\":[{\"skillName\":\"React\",\"proficiency\":4,\"yearsOfExperience\":2}]," +
        "\"experience\":[{\"company\":\"Initech\",\"title\":\"Dev\",\"startDate\":\"2019-01-01\",\"endDate\":\"2021-01-01\"}]," +
        "\"education\":[{\"institution\":\"MIT\",\"qualification\":\"BSc\",\"fieldOfStudy\":\"CS\"}]," +
        "\"certifications\":[{\"name\":\"AWS SAA\",\"issuer\":\"Amazon\"}]," +
        "\"languages\":[{\"name\":\"French\",\"proficiency\":\"Basic\"}]," +
        "\"compensation\":[{\"ctc\":1200000,\"bonus\":100000,\"currency\":\"INR\",\"effectiveFrom\":\"2020-01-01\",\"effectiveTo\":null}]}," +
        "{\"id\":2,\"employeeCode\":\"E2\",\"firstName\":\"Ben\",\"lastName\":\"Rao\",\"designationId\":100,\"managerEmployeeId\":1,\"employmentStatus\":\"ACTIVE\",\"career\":null," +
        "\"skills\":[],\"experience\":[],\"education\":[],\"certifications\":[],\"languages\":[],\"compensation\":[]}]," +
        "\"totalCount\":2,\"truncated\":false}";

    private static Routes BatchOnly() => new Routes()
        .Get("/api/employees/export-details", Batch)
        .Get("/api/employees/references", "[{\"id\":100,\"kind\":\"DESIGNATION\",\"name\":\"Engineer\"}]");

    [Fact]
    public async Task AllEmployees_UsesOneBatchCallInsteadOfReadingEachPerson()
    {
        var (handler, factory) = BatchOnly().Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "HR", caller: 99));

        Assert.True(result.Success, result.Error);
        
        Assert.Equal(2, handler.Calls.Count);
        Assert.DoesNotContain(handler.Calls, c => c.Request.RequestUri!.AbsolutePath.StartsWith("/api/employees/1") || c.Request.RequestUri!.AbsolutePath.StartsWith("/api/employees/2"));
        Assert.DoesNotContain(handler.Calls, c => c.Request.RequestUri!.Query.Contains("PageSize"));
    }

    [Fact]
    public async Task AllEmployees_GivesTheSameAnswerFromTheBatchAsFromReadingEachPerson()
    {
        var (_, batchFactory) = BatchOnly().Build();
        var (_, slowFactory) = Everyone().Build();

        var fast = await new GetAllEmployeesDetailsTool(batchFactory, Anyone).ExecuteAsync(Args("{}", roles: "HR", caller: 99));
        var slow = await new GetAllEmployeesDetailsTool(slowFactory, Anyone).ExecuteAsync(Args("{}", roles: "HR", caller: 99));

        using var a = Data(fast);
        using var b = Data(slow);
        string Sheet(JsonDocument d) => d.RootElement.GetProperty("file").GetProperty("content").GetString()!;
        
        Assert.Equal(Sheet(b), Sheet(a));
        var fastPeople = a.RootElement.GetProperty("employees").EnumerateArray().ToList();
        var slowPeople = b.RootElement.GetProperty("employees").EnumerateArray().ToList();
        Assert.Equal(slowPeople.Count, fastPeople.Count);
        for (var i = 0; i < fastPeople.Count; i++)
        {
            foreach (var key in new[] { "employeeCode", "name", "designation", "jobTitle", "department", "grade", "manager", "skills", "currentCtc" })
                Assert.Equal(slowPeople[i].TryGetProperty(key, out var s) ? s.ToString() : null, fastPeople[i].TryGetProperty(key, out var f) ? f.ToString() : null);
            Assert.Equal(slowPeople[i].GetProperty("counts").ToString(), fastPeople[i].GetProperty("counts").ToString());
        }
        Assert.False(fastPeople[1].TryGetProperty("partsNotRead", out var notRead) && notRead.ValueKind != JsonValueKind.Null);   
    }

    [Fact]
    public async Task AllEmployees_PassesTheStatusToTheBatchCall()
    {
        var (handler, factory) = BatchOnly().Build();

        await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{\"status\":\"active\"}", roles: "HR", caller: 99));

        Assert.Contains(handler.Calls, c => c.Request.RequestUri!.AbsolutePath == "/api/employees/export-details" && c.Request.RequestUri.Query.Contains("status=ACTIVE"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]   
    [InlineData(HttpStatusCode.NotFound)]    
    public async Task AllEmployees_FallsBackToReadingEachPersonWhenTheBatchCannotBeUsed(HttpStatusCode status)
    {
        var (handler, factory) = Everyone().Get("/api/employees/export-details", "{}", status).Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "HR", caller: 99));

        Assert.True(result.Success, result.Error);
        using var data = Data(result);
        Assert.Equal(2, data.RootElement.GetProperty("count").GetInt32());
        Assert.Contains(handler.Calls, c => c.Request.RequestUri!.AbsolutePath == "/api/employees/1/skills");
        Assert.Equal("1200000 INR", data.RootElement.GetProperty("employees")[0].GetProperty("currentCtc").GetString());
    }

    [Fact]
    public async Task AllEmployees_StillRefusesNonHrRolesBeforeTouchingTheBatch()
    {
        var (handler, factory) = BatchOnly().Build();

        var result = await new GetAllEmployeesDetailsTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "DEVELOPER", caller: 99));

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    

    private static Routes OtherPerson() => new Routes()
        .Get("/api/employees/2/skills", "[{\"skillName\":\"React\",\"category\":\"Frontend\",\"proficiency\":4,\"yearsOfExperience\":2,\"lastUsedDate\":null}]")
        .Get("/api/employees/2/education", "[{\"institution\":\"MIT\",\"qualification\":\"BSc\",\"fieldOfStudy\":\"CS\",\"startDate\":\"2018-08-01\",\"endDate\":null}]")
        .Get("/api/employees/2", "{\"id\":2,\"userId\":12,\"employeeCode\":\"E2\",\"firstName\":\"Ben\",\"lastName\":\"Rao\",\"phone\":\"999\",\"emergencyContactName\":\"Mum\",\"emergencyContactPhone\":\"111\"}")
        .Get("/api/employees/references", "[]")
        .Get("/api/employees?", People);

    [Theory]
    [InlineData("personal")]
    [InlineData("experience")]
    [InlineData("education")]
    [InlineData("certifications")]
    [InlineData("languages")]
    public async Task EmployeeRecord_ForSomeoneElse_IsRefusedToOrdinaryUsers_NotJustHidden(string section)
    {
        var (handler, factory) = OtherPerson().Build();

        var result = await new GetEmployeeRecordTool(factory, Anyone).ExecuteAsync(Args($"{{\"employeeId\":2,\"section\":\"{section}\"}}", roles: "DEVELOPER,MANAGER", caller: 99));

        Assert.False(result.Success);
        Assert.Contains("RBAC", result.Error);
        Assert.Contains("HR and Admin", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.RequestUri!.AbsolutePath.EndsWith($"/{section}"));   
    }

    [Theory]
    [InlineData("HR", 99)]
    [InlineData("ADMIN", 99)]
    [InlineData("DEVELOPER", 12)]   
    public async Task EmployeeRecord_IsOpenToHrAdminAndTheirOwner(string roles, long caller)
    {
        var (_, factory) = OtherPerson().Build();

        var result = await new GetEmployeeRecordTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2,\"section\":\"education\"}", roles: roles, caller: caller));

        using var data = Data(result);
        Assert.Equal("MIT", data.RootElement.GetProperty("items")[0].GetProperty("institution").GetString());
    }

    [Fact]
    public async Task Profile_OfSomeoneElse_ShowsNoSkillsPhoneOrEmergencyContact_ToOrdinaryUsers()
    {
        var (handler, factory) = OtherPerson().Build();

        var result = await new GetEmployeeProfileTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", roles: "DEVELOPER", caller: 99));

        using var data = Data(result);
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("skills").ValueKind);
        Assert.Contains("HR and Admin", data.RootElement.GetProperty("skillsNote").GetString());
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("phone").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("emergencyContact").ValueKind);
        Assert.DoesNotContain("React", data.RootElement.GetRawText());
        Assert.DoesNotContain(handler.Calls, c => c.Request.RequestUri!.AbsolutePath.EndsWith("/skills"));
    }

    [Theory]
    [InlineData("HR", 99)]
    [InlineData("DEVELOPER", 12)]
    public async Task Profile_ShowsSkillsPhoneAndEmergencyContact_ToHrAndToTheOwner(string roles, long caller)
    {
        var (_, factory) = OtherPerson().Build();

        var result = await new GetEmployeeProfileTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", roles: roles, caller: caller));

        using var data = Data(result);
        Assert.Equal("React", data.RootElement.GetProperty("skills")[0].GetProperty("skill").GetString());
        Assert.Equal("999", data.RootElement.GetProperty("phone").GetString());
        Assert.Equal("Mum", data.RootElement.GetProperty("emergencyContact").GetProperty("name").GetString());
    }

    [Fact]
    public async Task CareerHistory_StaysOpenToEveryone()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/2/career-history", Career)
            .Get("/api/employees/2", "{\"id\":2,\"userId\":12,\"firstName\":\"Ben\",\"lastName\":\"Rao\"}")
            .Build();

        var result = await new GetCareerHistoryTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", roles: "DEVELOPER", caller: 99));

        using var data = Data(result);
        Assert.Equal(2, data.RootElement.GetProperty("count").GetInt32());
    }

    

    [Fact]
    public async Task GetProfile_IncludesTheSkillsWithProficiencyLabels()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/2/skills", "[{\"skillName\":\"React\",\"category\":\"Frontend\",\"proficiency\":4,\"yearsOfExperience\":2,\"lastUsedDate\":\"2026-10-15\"},{\"skillName\":\"Angular\",\"category\":\"Frontend\",\"proficiency\":3,\"yearsOfExperience\":1,\"lastUsedDate\":null}]")
            .Get("/api/employees/2", "{\"id\":2,\"userId\":12,\"employeeCode\":\"E2\",\"firstName\":\"Ben\",\"lastName\":\"Rao\",\"employmentStatus\":\"ACTIVE\"}")
            .Get("/api/employees/references", "[]")
            .Get("/api/employees?", People)
            .Build();

        var result = await new GetEmployeeProfileTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", caller: 12));

        using var data = Data(result);
        var skills = data.RootElement.GetProperty("skills");
        Assert.Equal(2, skills.GetArrayLength());
        Assert.Equal("React", skills[0].GetProperty("skill").GetString());
        Assert.Equal("Advanced", skills[0].GetProperty("proficiencyLabel").GetString());
        Assert.Equal("Intermediate", skills[1].GetProperty("proficiencyLabel").GetString());
        Assert.Equal(2, skills[0].GetProperty("yearsOfExperience").GetDouble());
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("skillsNote").ValueKind);
    }

    [Fact]
    public async Task GetProfile_WhenSkillsCannotBeRead_SaysSoInsteadOfClaimingNone()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/2/skills", "{}", HttpStatusCode.Forbidden)
            .Get("/api/employees/2", "{\"id\":2,\"userId\":12,\"employeeCode\":\"E2\",\"firstName\":\"Ben\",\"lastName\":\"Rao\"}")
            .Get("/api/employees/references", "[]")
            .Get("/api/employees?", People)
            .Build();

        var result = await new GetEmployeeProfileTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", caller: 12));

        using var data = Data(result);
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("skills").ValueKind);
        Assert.Contains("do not claim they have none", data.RootElement.GetProperty("skillsNote").GetString());
    }


    [Fact]
    public async Task GetProfile_ShowsNamesManagerChainAndProjects_WithoutGovernmentIds()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/2", "{\"id\":2,\"userId\":12,\"employeeCode\":\"E2\",\"firstName\":\"Ben\",\"lastName\":\"Rao\",\"workEmail\":\"ben@x.com\",\"aadharNumber\":\"1234-5678\",\"permanentAddress\":\"secret road\",\"designationId\":100,\"managerEmployeeId\":1,\"employmentStatus\":\"ACTIVE\"}")
            .Get("/api/employees/references", "[{\"id\":100,\"kind\":\"DESIGNATION\",\"name\":\"Senior Engineer\"}]")
            .Get("/api/employees?", People)
            .Get("/api/projects/by-user/12", "[{\"id\":4,\"projectName\":\"Google04\",\"key\":\"G4\",\"role\":\"DEVELOPER\",\"status\":\"ACTIVE\"}]")
            .Build();

        var result = await new GetEmployeeProfileTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", caller: 12));

        using var data = Data(result);
        var text = (string)result.Data!;
        Assert.Equal("Senior Engineer", data.RootElement.GetProperty("designation").GetString());
        Assert.Equal("Asha Kumar", data.RootElement.GetProperty("manager").GetProperty("name").GetString());
        Assert.Equal("Google04", data.RootElement.GetProperty("projects")[0].GetProperty("name").GetString());
        Assert.DoesNotContain("1234-5678", text);
        Assert.DoesNotContain("secret road", text);
    }

    [Fact]
    public async Task GetProfile_WhenSeveralPeopleMatch_AsksWhichOne()
    {
        var (_, factory) = new Routes().Get("/api/employees?", People).Build();

        var result = await new GetEmployeeProfileTool(factory, Anyone).ExecuteAsync(Args("{\"query\":\"a\"}"));

        using var data = Data(result);
        Assert.True(data.RootElement.GetProperty("needsChoice").GetBoolean());
        Assert.Equal(2, data.RootElement.GetProperty("candidates").GetArrayLength());
    }

    [Theory]
    [InlineData("{\"restrictedDesignation\":\"Staff Engineer\"}", "Designation")]
    [InlineData("{\"restrictedCompensation\":\"+20%\"}", "CTC")]
    [InlineData("{\"restrictedManager\":\"Asha\"}", "Manager")]
    [InlineData("{\"restrictedBaseLocation\":\"Pune\"}", "Base Location")]
    public async Task UpdateMyProfile_BlocksRestrictedFields_WithoutCallingAnything(string json, string field)
    {
        var (handler, factory) = new Routes().Build();

        var result = await new UpdateMyProfileTool(factory, Anyone).ExecuteAsync(Args(json));

        Assert.False(result.Success);
        Assert.StartsWith("RBAC:", result.Error);
        Assert.Contains(field, result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task UpdateMyProfile_KeepsEverythingElseWhenChangingOneField()
    {
        var (handler, factory) = new Routes()
            .Get("/api/employees/me", "{\"id\":1,\"phone\":\"111\",\"locationId\":5,\"aadharNumber\":\"A-1\",\"permanentAddress\":\"Old St\",\"emergencyContactName\":\"Old Name\",\"preferredName\":\"Ash\",\"version\":4}")
            .Put("/api/employees/me/profile", "{\"id\":1,\"version\":5}")
            .Build();

        var result = await new UpdateMyProfileTool(factory, Anyone).ExecuteAsync(Args("{\"phone\":\"999\"}"));

        Assert.True(result.Success, result.Error);
        var put = handler.Calls.Single(c => c.Request.Method == HttpMethod.Put);
        Assert.Equal("\"4\"", put.Request.Headers.IfMatch.Single().ToString());
        using var body = JsonDocument.Parse(put.Body!);
        Assert.Equal("999", body.RootElement.GetProperty("phone").GetString());
        Assert.Equal(5, body.RootElement.GetProperty("locationId").GetInt64());
        Assert.Equal("A-1", body.RootElement.GetProperty("aadharNumber").GetString());
        Assert.Equal("Old St", body.RootElement.GetProperty("permanentAddress").GetString());
        Assert.Equal("Old Name", body.RootElement.GetProperty("emergencyContactName").GetString());
        Assert.Equal("Ash", body.RootElement.GetProperty("preferredName").GetString());
    }

    [Fact]
    public async Task UpdateMyProfile_AddsASkill_UsingTheVersionReturnedByTheProfileSave()
    {
        var (handler, factory) = new Routes()
            .Get("/api/employees/me", "{\"id\":1,\"version\":4}")
            .Put("/api/employees/me/profile", "{\"id\":1,\"version\":5}")
            .Post("/api/employees/me/skills", "{\"item\":{},\"version\":6}")
            .Build();

        var result = await new UpdateMyProfileTool(factory, Anyone).ExecuteAsync(Args("{\"phone\":\"999\",\"skillName\":\"Kubernetes\",\"skillProficiency\":4,\"skillYears\":2.5}"));

        Assert.True(result.Success, result.Error);
        var post = handler.Calls.Single(c => c.Request.Method == HttpMethod.Post);
        Assert.Equal("\"5\"", post.Request.Headers.IfMatch.Single().ToString());
        using var body = JsonDocument.Parse(post.Body!);
        Assert.Equal("Kubernetes", body.RootElement.GetProperty("skillName").GetString());
        Assert.Equal(4, body.RootElement.GetProperty("proficiency").GetInt32());
        Assert.Equal(2.5m, body.RootElement.GetProperty("yearsOfExperience").GetDecimal());
    }

    [Fact]
    public async Task UpdateMyProfile_AsksForProficiencyInsteadOfInventingIt_AndExplainsThereIsNoBio()
    {
        var (handler, factory) = new Routes().Build();
        var tool = new UpdateMyProfileTool(factory, Anyone);

        var noLevel = await tool.ExecuteAsync(Args("{\"skillName\":\"Rust\"}"));
        var bio = await tool.ExecuteAsync(Args("{\"bio\":\"I like code\"}"));

        Assert.False(noLevel.Success);
        Assert.Contains("1 (beginner) to 5", noLevel.Error);
        Assert.False(bio.Success);
        Assert.Contains("bio", bio.Error);
        Assert.Empty(handler.Calls);
    }

    

    [Fact]
    public async Task Dossier_IsLimitedToHrAndAdmin()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new ExportEmployeeDossierTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", roles: "DEVELOPER"));

        Assert.False(result.Success);
        Assert.StartsWith("RBAC:", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Dossier_AsPdf_IsDeclinedHonestly()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new ExportEmployeeDossierTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2,\"format\":\"pdf\"}", roles: "ADMIN"));

        Assert.False(result.Success);
        Assert.Contains("CSV", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Dossier_AsCsv_ContainsTheSections_DefusesFormulas_AndOmitsInternalIds()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/2/skills", "[{\"id\":1,\"tenantId\":1,\"employeeId\":2,\"skillName\":\"=HYPERLINK(\\\"x\\\")\",\"proficiency\":4}]")
            .Get("/api/employees/2/compensation", "{}", HttpStatusCode.Forbidden)
            .Get("/api/employees/2", "{\"id\":2,\"userId\":12,\"employeeCode\":\"E2\",\"firstName\":\"Ben\",\"lastName\":\"Rao\",\"aadharNumber\":\"1234-5678\"}")
            .Get("/api/employees/references", "[]")
            .Get("/api/employees?", People)
            .Build();

        var result = await new ExportEmployeeDossierTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":2}", roles: "HR"));

        using var data = Data(result);
        var file = data.RootElement.GetProperty("file");
        var csv = file.GetProperty("content").GetString()!;
        Assert.Equal("text/csv", file.GetProperty("mimeType").GetString());
        Assert.EndsWith(".csv", file.GetProperty("fileName").GetString());
        Assert.StartsWith("section,item,field,value", csv);
        Assert.Contains("Ben Rao", csv);
        Assert.Contains("'=HYPERLINK", csv);
        Assert.DoesNotContain("tenantId", csv);
        Assert.DoesNotContain("1234-5678", csv);
        Assert.Contains("compensation", data.RootElement.GetProperty("sectionsNotAvailable").EnumerateArray().Select(s => s.GetString()));
    }

    

    [Fact]
    public async Task CompensationMatrix_IsLimitedToHrFinanceAndAdmin()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new GetCompensationMatrixTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "DEVELOPER,PROJECT_MANAGER"));

        Assert.False(result.Success);
        Assert.StartsWith("RBAC:", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task CompensationMatrix_AggregatesByBand_AndNeverNamesAnyone()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/1/compensation", "[{\"ctc\":1000000,\"bonus\":100000,\"currency\":\"INR\",\"effectiveFrom\":\"2020-01-01\",\"effectiveTo\":null}]")
            .Get("/api/employees/2/compensation", "[{\"ctc\":2000000,\"bonus\":0,\"currency\":\"INR\",\"effectiveFrom\":\"2020-01-01\",\"effectiveTo\":null}]")
            .Get("/api/employees/references", "[{\"id\":100,\"kind\":\"DESIGNATION\",\"name\":\"Senior Engineer\"}]")
            .Get("/api/employees?", People)
            .Build();

        var result = await new GetCompensationMatrixTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "FINANCE"));

        using var data = Data(result);
        var band = data.RootElement.GetProperty("bands").EnumerateArray().Single();
        Assert.Equal("Senior Engineer", band.GetProperty("band").GetString());
        Assert.Equal(2, band.GetProperty("headcount").GetInt32());
        Assert.Equal(1000000, band.GetProperty("ctcMin").GetDecimal());
        Assert.Equal(1500000, band.GetProperty("ctcAverage").GetDecimal());
        Assert.Equal(2000000, band.GetProperty("ctcMax").GetDecimal());
        Assert.Equal("INR", band.GetProperty("currency").GetString());
        Assert.Equal("2 compensation records", band.GetProperty("basedOn").GetString());
        Assert.Contains("never tell the user it is someone's band", data.RootElement.GetProperty("note").GetString());
        Assert.DoesNotContain("Asha", (string)result.Data!);
        Assert.DoesNotContain("Ben", (string)result.Data!);
    }

    [Fact]
    public async Task CompensationMatrix_ABandOfOnePerson_SaysSo()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/1/compensation", "[{\"ctc\":1200000,\"bonus\":100000,\"currency\":\"INR\",\"effectiveFrom\":\"2020-01-01\",\"effectiveTo\":null}]")
            .Get("/api/employees/2/compensation", "[]")
            .Get("/api/employees/references", "[{\"id\":100,\"kind\":\"DESIGNATION\",\"name\":\"Junior\"}]")
            .Get("/api/employees?", People)
            .Build();

        var result = await new GetCompensationMatrixTool(factory, Anyone).ExecuteAsync(Args("{}", roles: "HR"));

        using var data = Data(result);
        Assert.Equal(2, data.RootElement.GetProperty("employeesInDirectory").GetInt32());
        Assert.Equal(1, data.RootElement.GetProperty("employeesWithCurrentCompensation").GetInt32());
        Assert.Equal(1, data.RootElement.GetProperty("employeesWithoutCompensation").GetInt32());
        Assert.Contains("1 compensation record", data.RootElement.GetProperty("bands")[0].GetProperty("basedOn").GetString());
    }

    [Fact]
    public async Task EmployeeCompensation_IsRefusedForNonHrRoles()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new GetEmployeeCompensationTool(factory, Anyone).ExecuteAsync(Args("{\"query\":\"Asha\"}", roles: "DEVELOPER"));

        Assert.False(result.Success);
        Assert.StartsWith("RBAC:", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task EmployeeCompensation_ShowsTheCurrentRecordAndHistoryForHr()
    {
        var (_, factory) = new Routes()
            .Get("/api/employees/1/compensation", "[{\"ctc\":900000,\"bonus\":50000,\"currency\":\"INR\",\"effectiveFrom\":\"2018-01-01\",\"effectiveTo\":\"2019-12-31\"},{\"ctc\":1200000,\"bonus\":100000,\"currency\":\"INR\",\"effectiveFrom\":\"2020-01-01\",\"effectiveTo\":null}]")
            .Get("/api/employees/1", "{\"id\":1,\"firstName\":\"Asha\",\"lastName\":\"Rao\",\"employeeCode\":\"EMP-1\"}")
            .Get("/api/employees?", People)
            .Build();

        var result = await new GetEmployeeCompensationTool(factory, Anyone).ExecuteAsync(Args("{\"employeeId\":1}", roles: "HR"));

        using var data = Data(result);
        Assert.Equal("Asha Rao", data.RootElement.GetProperty("employee").GetString());
        Assert.Equal(1200000, data.RootElement.GetProperty("current").GetProperty("ctc").GetDecimal());
        Assert.Equal(2, data.RootElement.GetProperty("history").GetArrayLength());
    }

    

    [Fact]
    public async Task TimesheetHistory_ComparesEachWeekWithTheEightHourDay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var lastMonday = thisMonday.AddDays(-7);
        string D(int offset) => lastMonday.AddDays(offset).ToString("yyyy-MM-dd");

        var report = "{\"dailyTotals\":[" +
                     $"{{\"date\":\"{D(0)}\",\"minutes\":480}},{{\"date\":\"{D(1)}\",\"minutes\":480}},{{\"date\":\"{D(2)}\",\"minutes\":240}},{{\"date\":\"{D(4)}\",\"minutes\":480}}]," +
                     $"\"periods\":[{{\"id\":3,\"periodStart\":\"{D(0)}\",\"periodEnd\":\"{D(6)}\",\"status\":\"SUBMITTED\",\"totalMinutes\":1680,\"version\":2}}]}}";
        var (_, factory) = new Routes().Get("/api/timesheets/me", report).Get("/api/employees/absence/policy", "{\"holidays\":[]}").Build();

        var result = await new GetTimesheetHistoryTool(factory, Anyone).ExecuteAsync(Args("{\"weeks\":2}"));

        using var data = Data(result);
        var weeks = data.RootElement.GetProperty("weeks");
        Assert.Equal(2, weeks.GetArrayLength());
        var previous = weeks[1];
        Assert.Equal(D(0), previous.GetProperty("weekStart").GetString());
        Assert.Equal("SUBMITTED", previous.GetProperty("status").GetString());
        Assert.Equal(28, previous.GetProperty("loggedHours").GetDouble());
        Assert.Equal(40, previous.GetProperty("targetHours").GetDouble());
        Assert.Equal(12, previous.GetProperty("missingHours").GetDouble());
        Assert.Equal(8.0, data.RootElement.GetProperty("dailyTargetHours").GetDouble());
    }

    [Fact]
    public async Task TimesheetHistory_CompanyHolidaysAreNotCountedAgainstTheTarget()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var lastMonday = thisMonday.AddDays(-7);
        var holiday = lastMonday.AddDays(2).ToString("yyyy-MM-dd");
        var report = "{\"dailyTotals\":[],\"periods\":[]}";
        var (_, factory) = new Routes().Get("/api/timesheets/me", report).Get("/api/employees/absence/policy", $"{{\"holidays\":[\"{holiday}\"]}}").Build();

        var result = await new GetTimesheetHistoryTool(factory, Anyone).ExecuteAsync(Args("{\"weeks\":2}"));

        using var data = Data(result);
        var previous = data.RootElement.GetProperty("weeks")[1];
        Assert.Equal(32, previous.GetProperty("targetHours").GetDouble());
        Assert.Equal("NOT_STARTED", previous.GetProperty("status").GetString());
    }

    private const string TeamPeriods = "{\"periods\":[" +
        "{\"id\":11,\"userId\":12,\"periodStart\":\"2026-11-02\",\"periodEnd\":\"2026-11-08\",\"status\":\"SUBMITTED\",\"version\":3,\"totalMinutes\":2400,\"billableMinutes\":2000,\"projects\":[{\"projectName\":\"FLP360\",\"minutes\":2400}]}," +
        "{\"id\":12,\"userId\":7,\"periodStart\":\"2026-11-02\",\"periodEnd\":\"2026-11-08\",\"status\":\"SUBMITTED\",\"version\":1,\"totalMinutes\":100}," +
        "{\"id\":13,\"userId\":11,\"status\":\"APPROVED\",\"version\":1}]}";

    [Fact]
    public async Task ApproveTimesheet_List_ExcludesTheCallersOwnAndAlreadyDecidedPeriods()
    {
        var (_, factory) = new Routes().Get("/api/timesheets/team", TeamPeriods).Get("/api/employees?", People).Build();

        var result = await new ApproveTimesheetPeriodTool(factory, Anyone).ExecuteAsync(Args("{}", caller: 7));

        using var data = Data(result);
        Assert.Equal(1, data.RootElement.GetProperty("pendingCount").GetInt32());
        var period = data.RootElement.GetProperty("periods")[0];
        Assert.Equal("Ben Rao", period.GetProperty("employee").GetString());
        Assert.Equal(40, period.GetProperty("loggedHours").GetDouble());
    }

    [Fact]
    public async Task ApproveTimesheet_Approve_SendsThePeriodVersionInIfMatch()
    {
        var (handler, factory) = new Routes().Get("/api/timesheets/team", TeamPeriods).Get("/api/employees?", People).Post("/api/timesheets/approve", "{\"id\":11,\"status\":\"APPROVED\"}").Build();

        var result = await new ApproveTimesheetPeriodTool(factory, Anyone).ExecuteAsync(Args("{\"action\":\"approve\",\"periodId\":11}", caller: 7));

        var post = handler.Calls.Single(c => c.Request.Method == HttpMethod.Post);
        Assert.Equal("\"3\"", post.Request.Headers.IfMatch.Single().ToString());
        using var body = JsonDocument.Parse(post.Body!);
        Assert.Equal(11, body.RootElement.GetProperty("periodId").GetInt64());
        using var data = Data(result);
        Assert.Equal("APPROVED", data.RootElement.GetProperty("decision").GetString());
    }

    [Fact]
    public async Task ApproveTimesheet_RejectNeedsAReason_AndOwnPeriodsCannotBeReviewed()
    {
        var (handler, factory) = new Routes().Get("/api/timesheets/team", TeamPeriods).Get("/api/employees?", People).Build();
        var tool = new ApproveTimesheetPeriodTool(factory, Anyone);

        var noReason = await tool.ExecuteAsync(Args("{\"action\":\"reject\",\"periodId\":11}", caller: 7));
        var own = await tool.ExecuteAsync(Args("{\"action\":\"approve\",\"periodId\":12}", caller: 7));

        Assert.False(noReason.Success);
        Assert.Contains("reason", noReason.Error);
        Assert.False(own.Success);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task ApproveTimesheet_WithoutTeamRights_ExplainsTheRestriction()
    {
        var (_, factory) = new Routes().Get("/api/timesheets/team", "{}", HttpStatusCode.Forbidden).Build();

        var result = await new ApproveTimesheetPeriodTool(factory, Anyone).ExecuteAsync(Args("{}", caller: 7));

        Assert.False(result.Success);
        Assert.StartsWith("RBAC:", result.Error);
    }

    

    [Fact]
    public void ToolPayload_UnwrapsJsonThatWasSerialisedTwice()
    {
        var once = "{\"issueKey\":\"A-1\"}";
        var twice = JsonSerializer.Serialize(once);

        var unpacked = ToolPayload.Unpack(twice);

        Assert.Equal("{\"issueKey\":\"A-1\"}", JsonSerializer.Serialize(unpacked));
    }

    [Fact]
    public void ToolPayload_LeavesPlainTextAlone()
    {
        Assert.Equal("Issue created.", ToolPayload.Unpack("Issue created."));
    }

    [Fact]
    public void ToolPayload_KeepsFileContentAwayFromTheModel()
    {
        var unpacked = ToolPayload.Unpack("{\"exported\":true,\"file\":{\"fileName\":\"a.csv\",\"content\":\"SECRET-SALARY-DATA\"}}");

        var forModel = JsonSerializer.Serialize(ToolPayload.ForModel(unpacked));

        Assert.DoesNotContain("SECRET-SALARY-DATA", forModel);
        Assert.Contains("contentWithheldFromAssistant", forModel);
        Assert.Contains("a.csv", forModel);
        Assert.Contains("SECRET-SALARY-DATA", JsonSerializer.Serialize(unpacked));
    }
}


public class CallerContextOrchestratorTests
{
    private sealed class ProbeTool(string name, string payload) : IAgentTool
    {
        public string Name => name;
        public string Description => "x";
        public string Category => "Test";
        public string RequiredPermission => "";
        public object ParameterSchema => new { };
        public Dictionary<string, object?>? Seen { get; private set; }

        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Seen = arguments;
            return Task.FromResult(new ToolResult(true, payload));
        }
    }

    private static async Task<(AgentChatResponse Response, List<string> ToolMessages)> Run(ProbeTool tool, string modelArguments, string role = "HR", long userId = 21)
    {
        var step = 0;
        var toolMessages = new List<string>();
        var llm = new MockLlmProvider
        {
            OnChat = (msgs, _) =>
            {
                toolMessages.AddRange(msgs.Where(m => m.Role == AIAgent.Domain.Models.MessageRole.Tool).Select(m => m.Content));
                return ++step == 1
                    ? new LlmChatResponse { ToolCalls = [new ToolCall { Id = "1", Name = tool.Name, ArgumentsJson = modelArguments }] }
                    : new LlmChatResponse { Content = "done" };
            }
        };
        var context = new Dictionary<string, object> { ["role"] = role, ["userId"] = userId, ["permissions"] = Array.Empty<string>() };
        var response = await new AthennaAgentOrchestrator(llm, [tool]).ExecuteChatAsync(new AgentChatRequest("go", ExtraContext: context));
        return (response, toolMessages);
    }

    [Fact]
    public async Task Tools_ReceiveTheCallersRolesAndId_FromTheServer()
    {
        var tool = new ProbeTool("probe", "{}");

        await Run(tool, "{}", role: "HR", userId: 21);

        Assert.Equal(21L, Convert.ToInt64(tool.Seen![IssueVisibility.CallerUserIdArgument]));
        Assert.Contains("HR", tool.Seen[IssueVisibility.CallerRolesArgument]!.ToString()!.Split(','));
    }

    [Fact]
    public async Task TheModel_CannotGrantItselfARole()
    {
        var tool = new ProbeTool("probe", "{}");

        await Run(tool, $"{{\"{IssueVisibility.CallerRolesArgument}\":\"ADMIN\",\"{IssueVisibility.CallerUserIdArgument}\":1}}", role: "DEVELOPER", userId: 21);

        Assert.DoesNotContain("ADMIN", tool.Seen![IssueVisibility.CallerRolesArgument]!.ToString()!.Split(','));
        Assert.Equal(21L, Convert.ToInt64(tool.Seen[IssueVisibility.CallerUserIdArgument]));
    }

    [Fact]
    public async Task DownloadableFiles_ReachTheUi_ButNotTheModel()
    {
        var tool = new ProbeTool("export_probe", "{\"file\":{\"fileName\":\"d.csv\",\"mimeType\":\"text/csv\",\"content\":\"TOP-SECRET\"}}");

        var (response, toolMessages) = await Run(tool, "{}");

        Assert.DoesNotContain(toolMessages, m => m.Contains("TOP-SECRET"));
        var action = Assert.Single(response.ExecutedActions!);
        Assert.Contains("TOP-SECRET", JsonSerializer.Serialize(action.Result));
    }

    [Fact]
    public async Task NestedJsonInToolData_IsDeliveredToTheUiAsRealJson()
    {
        var tool = new ProbeTool("nested_probe", JsonSerializer.Serialize("{\"issueKey\":\"A-1\",\"summary\":\"s\"}"));

        var (response, _) = await Run(tool, "{}");

        var action = Assert.Single(response.ExecutedActions!);
        Assert.Contains("\"issueKey\":\"A-1\"", JsonSerializer.Serialize(action.Result));
        Assert.DoesNotContain("\\\"issueKey\\\"", JsonSerializer.Serialize(action.Result));
    }
}

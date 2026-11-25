using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;


internal sealed class FakeSprints
{
    public static DateOnly Today => FakeChronos.Today;

    public string ProjectsJson = """[{"id":9,"key":"FLP","displayName":"Flagship"},{"id":10,"key":"OPS","displayName":"Operations"}]""";
    public Dictionary<long, string> BoardsByProject = new()
    {
        [9] = """[{"id":100,"boardType":"SCRUM","name":"Flagship board"}]""",
        [10] = """[{"id":200,"boardType":"KANBAN","name":"Ops board"}]""",
    };
    public Dictionary<long, string> SprintsByBoard = new();
    public List<string> Issues = [];
    public List<long> InSprint = [];

    public static string Sprint(long id, string name, string status, int startOffset, int endOffset, string? goal = "Ship the new login") =>
        $"{{\"id\":{id},\"name\":\"{name}\",\"status\":\"{status}\",\"goal\":{(goal == null ? "null" : $"\"{goal}\"")},\"startDate\":\"{Today.AddDays(startOffset):yyyy-MM-dd}\",\"endDate\":\"{Today.AddDays(endOffset):yyyy-MM-dd}\",\"createdAt\":\"2026-11-0{id % 9 + 1}T00:00:00Z\"}}";

    public static string Issue(long id, string status, int? points, long? assignee, int? dueOffset = null, string priority = "MEDIUM", long projectId = 9) =>
        $"{{\"id\":{id},\"issueKey\":\"FLP-{id}\",\"summary\":\"Issue {id}\",\"statusCode\":\"{status}\",\"priority\":\"{priority}\",\"projectId\":{projectId},\"storyPoints\":{(points == null ? "null" : points.ToString())},\"assigneeUserId\":{(assignee == null ? "null" : assignee.ToString())},\"assigneeUserIds\":[{(assignee == null ? "" : assignee.ToString())}],\"dueDate\":{(dueOffset == null ? "null" : $"\"{Today.AddDays(dueOffset.Value):yyyy-MM-dd}\"")}}}";

    public RecordingHandler Handler { get; }

    public FakeSprints()
    {
        Handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;
            if (path == "/api/projects" || path.StartsWith("/api/projects/by-user/")) return Http.Json(ProjectsJson);
            if (path == "/api/boards")
            {
                var id = long.Parse(System.Web.HttpUtility.ParseQueryString(query)["projectId"]!);
                return Http.Json(BoardsByProject.GetValueOrDefault(id, "[]"));
            }
            if (path.EndsWith("/sprints")) return Http.Json(SprintsByBoard.GetValueOrDefault(long.Parse(path.Split('/')[3]), "[]"));
            if (path.EndsWith("/backlog"))
                return Http.Json($"{{\"items\":[{string.Join(",", InSprint.Select(i => $"{{\"id\":{i}}}"))}],\"page\":1,\"pageSize\":50,\"totalCount\":{InSprint.Count}}}");
            if (path == "/api/issues/search")
            {
                var wanted = System.Web.HttpUtility.ParseQueryString(query)["AssigneeUserId"];
                var items = Issues.Where(i => wanted == null || JsonDocument.Parse(i).RootElement.GetProperty("assigneeUserId").ToString() == wanted);
                return Http.Json($"{{\"items\":[{string.Join(",", items)}],\"page\":1,\"pageSize\":100,\"totalCount\":{Issues.Count}}}");
            }
            if (path == "/api/employees")
                return Http.Json("{\"items\":[" + string.Join(",", new[] { ("5", "Asha", "Kumar"), ("6", "Priya", "Nair"), ("7", "Ravi", "Menon") }.Select((p, i) => $"{{\"id\":{100 + i},\"userId\":{p.Item1},\"firstName\":\"{p.Item2}\",\"lastName\":\"{p.Item3}\",\"employeeCode\":\"E{i}\"}}")) + "],\"totalPages\":1}");
            return Http.Json("{}", HttpStatusCode.NotFound);
        });
    }

    
    public static FakeSprints WithActiveSprint(int startOffset, int endOffset, params string[] issues)
    {
        var fake = new FakeSprints();
        fake.SprintsByBoard[100] = "[" + Sprint(1, "Sprint 12", "ACTIVE", startOffset, endOffset) + "," + Sprint(2, "Sprint 11", "COMPLETED", startOffset - 14, startOffset - 1) + "]";
        fake.Issues = [.. issues];
        fake.InSprint = issues.Select(i => JsonDocument.Parse(i).RootElement.GetProperty("id").GetInt64()).ToList();
        return fake;
    }

    public T Tool<T>() where T : class => (T)Activator.CreateInstance(typeof(T), new FakeHttpClientFactory(Handler), new HttpContextAccessor())!;
    public IEnumerable<string> Urls => Handler.Calls.Select(c => c.Request.RequestUri!.PathAndQuery);
}

public class SprintOverviewToolTests
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs)
    {
        var args = new Dictionary<string, object?> { [IssueVisibility.CallerUserIdArgument] = 5L, [IssueVisibility.CallerRolesArgument] = "MANAGER" };
        foreach (var (key, value) in pairs) args[key] = value;
        return args;
    }

    private static JsonElement Data(ToolResult result) => JsonDocument.Parse(result.Data!.ToString()!).RootElement;
    private static string[] Signals(JsonElement data) => data.GetProperty("signals").EnumerateArray().Select(s => s.GetString()!).ToArray();

    private static FakeSprints Typical(int startOffset = -4, int endOffset = 9) => FakeSprints.WithActiveSprint(startOffset, endOffset,
        FakeSprints.Issue(1, "DONE", 3, 5),
        FakeSprints.Issue(2, "DONE", 5, 6),
        FakeSprints.Issue(3, "IN_PROGRESS", 5, 6),
        FakeSprints.Issue(4, "TODO", 3, 6, dueOffset: -1, priority: "HIGH"),
        FakeSprints.Issue(5, "TODO", 2, null),
        FakeSprints.Issue(6, "CANCELLED", 8, 7));

    [Fact]
    public async Task DescribesTheActiveSprint_WithFiguresWorkedOutFromTheIssues()
    {
        var fake = Typical();

        var result = await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args());

        Assert.True(result.Success, result.Error);
        var data = Data(result);
        Assert.True(data.GetProperty("overview").GetBoolean());
        Assert.Equal("Flagship", data.GetProperty("project").GetProperty("name").GetString());
        Assert.Equal("Sprint 12", data.GetProperty("sprint").GetProperty("name").GetString());
        Assert.Equal("Ship the new login", data.GetProperty("sprint").GetProperty("goal").GetString());
        Assert.Equal(9, data.GetProperty("sprint").GetProperty("daysLeft").GetInt32());

        var issues = data.GetProperty("issues");
        Assert.Equal(5, issues.GetProperty("total").GetInt32()); 
        Assert.Equal(2, issues.GetProperty("done").GetInt32());
        Assert.Equal(3, issues.GetProperty("open").GetInt32());
        Assert.Equal(1, issues.GetProperty("cancelled").GetInt32());
        Assert.Equal(2, issues.GetProperty("byStatus").GetProperty("TODO").GetInt32());

        var points = data.GetProperty("points");
        Assert.Equal(18, points.GetProperty("total").GetDouble());
        Assert.Equal(8, points.GetProperty("done").GetDouble());
        Assert.Equal(10, points.GetProperty("remaining").GetDouble());
        Assert.Equal(44, points.GetProperty("donePercent").GetDouble());
    }

    [Fact]
    public async Task FlagsOverdueAndUnassignedWork_AndListsThoseIssues()
    {
        var fake = Typical();

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.Contains(Signals(data), s => s.Contains("1 open issue is past its due date"));
        Assert.Contains(Signals(data), s => s.Contains("1 open issue has no assignee"));
        var items = data.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["FLP-4", "FLP-5"], items.Select(i => i.GetProperty("issueKey").GetString()!).ToArray());
        Assert.Equal("overdue by 1 day", items[0].GetProperty("why").GetString());
        Assert.Equal("Priya Nair", items[0].GetProperty("assignee").GetString());
        Assert.Equal("nobody", items[1].GetProperty("assignee").GetString());
    }

    [Fact]
    public async Task WorkloadShowsWhoHoldsTheOpenWork_ByName()
    {
        var fake = Typical();

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        var workload = data.GetProperty("workload").EnumerateArray().ToList();
        var priya = Assert.Single(workload);
        Assert.Equal("Priya Nair", priya.GetProperty("name").GetString());
        Assert.Equal(2, priya.GetProperty("openIssues").GetInt32());
        Assert.Equal(8, priya.GetProperty("openPoints").GetDouble());
    }

    [Fact]
    public async Task WarnsWhenTheSprintIsBehindPace()
    {
        var fake = Typical(startOffset: -10, endOffset: 3); 

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.Equal(79, data.GetProperty("sprint").GetProperty("timeElapsedPercent").GetDouble());
        Assert.Contains(Signals(data), s => s.StartsWith("Behind pace: 44% of the points are done with 79% of the time gone"));
    }

    [Fact]
    public async Task DoesNotWarnAboutPace_WhenWorkIsKeepingUp()
    {
        var fake = FakeSprints.WithActiveSprint(-4, 9,
            FakeSprints.Issue(1, "DONE", 5, 5), FakeSprints.Issue(2, "DONE", 5, 6), FakeSprints.Issue(3, "IN_PROGRESS", 3, 6));

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.DoesNotContain(Signals(data), s => s.StartsWith("Behind pace"));
        Assert.Empty(data.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task WarnsWhenTheSprintHasRunPastItsEnd()
    {
        var fake = Typical(startOffset: -16, endOffset: -2);

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.Contains(Signals(data), s => s.StartsWith("The sprint ended 2 days ago but is still active, with 3 open issues"));
    }

    [Fact]
    public async Task WarnsWhenWorkHasNotStartedAndTheSprintIsAboutToEnd()
    {
        var fake = Typical(startOffset: -12, endOffset: 1);

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.Contains(Signals(data), s => s == "2 issues have not been started with 1 day left.");
    }

    [Fact]
    public async Task IssuesWithoutPoints_AreSaidToBeLeftOutOfThePointsFigures()
    {
        var fake = FakeSprints.WithActiveSprint(-4, 9, FakeSprints.Issue(1, "DONE", 5, 5), FakeSprints.Issue(2, "TODO", null, 6));

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.Equal(1, data.GetProperty("points").GetProperty("unestimatedIssues").GetInt32());
        Assert.Contains(Signals(data), s => s.Contains("no story points"));
    }

    [Fact]
    public async Task NoActiveSprint_SaysSo_AndListsRecentOnes()
    {
        var fake = new FakeSprints();
        fake.SprintsByBoard[100] = "[" + FakeSprints.Sprint(2, "Sprint 11", "COMPLETED", -20, -7) + "," + FakeSprints.Sprint(3, "Sprint 13", "PLANNED", 1, 14) + "]";

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.False(data.GetProperty("overview").GetBoolean());
        Assert.Contains("no active sprint", data.GetProperty("note").GetString());
        Assert.Equal(2, data.GetProperty("sprints").GetArrayLength());
    }

    [Fact]
    public async Task SeveralProjectsWithActiveSprints_AskWhichOne()
    {
        var fake = new FakeSprints();
        fake.BoardsByProject[10] = """[{"id":200,"boardType":"SCRUM","name":"Ops board"}]""";
        fake.SprintsByBoard[100] = "[" + FakeSprints.Sprint(1, "Sprint 12", "ACTIVE", -4, 9) + "]";
        fake.SprintsByBoard[200] = "[" + FakeSprints.Sprint(5, "Ops sprint 3", "ACTIVE", -2, 5) + "]";

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.True(data.GetProperty("needsChoice").GetBoolean());
        Assert.Equal(["Flagship", "Operations"], data.GetProperty("activeSprints").EnumerateArray().Select(s => s.GetProperty("project").GetString()!).ToArray());
    }

    [Fact]
    public async Task ANamedProject_NarrowsTheSearch()
    {
        var fake = new FakeSprints();
        fake.BoardsByProject[10] = """[{"id":200,"boardType":"SCRUM","name":"Ops board"}]""";
        fake.SprintsByBoard[100] = "[" + FakeSprints.Sprint(1, "Sprint 12", "ACTIVE", -4, 9) + "]";
        fake.SprintsByBoard[200] = "[" + FakeSprints.Sprint(5, "Ops sprint 3", "ACTIVE", -2, 5) + "]";

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args(("project", "ops"))));

        Assert.True(data.GetProperty("overview").GetBoolean());
        Assert.Equal("Ops sprint 3", data.GetProperty("sprint").GetProperty("name").GetString());
        Assert.DoesNotContain(fake.Urls, u => u.StartsWith("/api/boards?projectId=9"));
    }

    [Fact]
    public async Task AnUnknownProject_ListsTheOnesThatExist()
    {
        var fake = new FakeSprints();

        var result = await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args(("project", "Zebra")));

        Assert.False(result.Success);
        Assert.Contains("FLP", result.Error);
        Assert.Contains("OPS", result.Error);
    }

    [Fact]
    public async Task AKanbanProject_HasNoSprints_AndSaysWhy()
    {
        var fake = new FakeSprints();

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args(("project", "OPS"))));

        Assert.False(data.GetProperty("overview").GetBoolean());
        Assert.Contains("Kanban", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ASprintCanBeAskedForByName()
    {
        var fake = Typical();

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args(("sprint", "sprint 11"))));

        Assert.Equal("Sprint 11", data.GetProperty("sprint").GetProperty("name").GetString());
        Assert.Equal("COMPLETED", data.GetProperty("sprint").GetProperty("status").GetString());
    }

    [Fact]
    public async Task AnUnknownSprintName_ListsTheSprintsThatExist()
    {
        var fake = Typical();

        var result = await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args(("sprint", "Sprint 99")));

        Assert.False(result.Success);
        Assert.Contains("Sprint 12 (active)", result.Error);
    }

    [Fact]
    public async Task WithOnlyACompletedSprint_ThereIsNoOverview_UnlessItIsAskedFor()
    {
        var fake = Typical();
        fake.SprintsByBoard[100] = "[" + FakeSprints.Sprint(2, "Sprint 11", "COMPLETED", -20, -7) + "]";

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.False(data.GetProperty("overview").GetBoolean()); 
    }

    [Fact]
    public async Task SomeoneWhoSeesOnlyTheirOwnIssues_GetsOnlyTheirPartOfTheSprint()
    {
        var fake = Typical();
        var args = Args();
        args[IssueVisibility.OwnerArgument] = 6L;

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(args));

        Assert.Contains(fake.Urls, u => u.StartsWith("/api/issues/search") && u.Contains("AssigneeUserId=6"));
        Assert.Equal("your issues only", data.GetProperty("scope").GetString());
        Assert.Contains("only the issues assigned to you", data.GetProperty("note").GetString());
        Assert.Equal(3, data.GetProperty("issues").GetProperty("total").GetInt32()); 
        Assert.Equal("You", data.GetProperty("workload")[0].GetProperty("name").GetString());
        Assert.DoesNotContain(data.GetProperty("items").EnumerateArray(), i => i.GetProperty("issueKey").GetString() == "FLP-5");
        Assert.DoesNotContain(fake.Urls, u => u.StartsWith("/api/employees")); 
    }

    [Fact]
    public async Task AnEmptySprint_SaysSo()
    {
        var fake = FakeSprints.WithActiveSprint(-1, 12);

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.Equal(0, data.GetProperty("issues").GetProperty("total").GetInt32());
        Assert.Equal("No issues are in this sprint yet.", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task IssuesThatCouldNotBeRead_AreMentioned()
    {
        var fake = Typical();
        fake.InSprint.Add(999);

        var data = Data(await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args()));

        Assert.Contains("1 issue in the sprint could not be read", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task WhenProjectsCannotBeListed_ItSaysSo()
    {
        var fake = new FakeSprints();
        fake.ProjectsJson = "{}";

        var result = await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(Args());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task WithoutAKnownCaller_ItRefuses()
    {
        var fake = Typical();

        var result = await fake.Tool<GetSprintOverviewTool>().ExecuteAsync(new Dictionary<string, object?> { [IssueVisibility.UnknownCallerArgument] = true });

        Assert.False(result.Success);
        Assert.Empty(fake.Urls);
    }
}

using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;


internal sealed class FakeJira
{
    public long IssueId = 42;
    public string IssueJson = """{"id":42,"issueKey":"FLP-7","summary":"Fix login","description":"Users cannot sign in.","statusCode":"TODO","priority":"MEDIUM","projectId":9,"assigneeUserId":5,"assigneeUserIds":[5],"dueDate":null,"storyPoints":null,"version":3}""";
    public long[] Members = [5, 6, 7, 8];
    public HttpStatusCode PutStatus = HttpStatusCode.OK;
    public string? PutBody;

    public (string UserId, string First, string Last, string Email)[] People =
    [
        ("5", "Asha", "Kumar", "asha@x.test"),
        ("6", "Priya", "Nair", "priya@x.test"),
        ("7", "Priya", "Shah", "priyas@x.test"),
        ("8", "Ravi", "Menon", "ravi@x.test"),
    ];

    public RecordingHandler Handler { get; }

    public FakeJira()
    {
        Handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put)
            {
                var body = Handler!.Calls.Last().Body;
                PutBody = body;
                return PutStatus == HttpStatusCode.OK ? Http.Json(UpdatedJson(body)) : Http.Json("""{"message":"Not allowed by the workflow."}""", PutStatus);
            }
            if (path.StartsWith("/api/issues/key/")) return Http.Json($"{{\"id\":{IssueId}}}");
            if (path == $"/api/issues/{IssueId}") return Http.Json(IssueJson);
            if (path.EndsWith("/assignment-members")) return Http.Json("[" + string.Join(",", Members.Select(m => $"{{\"userId\":{m}}}")) + "]");
            if (path == "/api/employees")
                return Http.Json("{\"items\":[" + string.Join(",", People.Select((p, i) => $"{{\"id\":{100 + i},\"userId\":{p.UserId},\"firstName\":\"{p.First}\",\"lastName\":\"{p.Last}\",\"workEmail\":\"{p.Email}\",\"employeeCode\":\"E{i}\"}}")) + "],\"totalPages\":1}");
            return Http.Json("{}", HttpStatusCode.NotFound);
        });
    }

    
    private string UpdatedJson(string? body)
    {
        var issue = System.Text.Json.Nodes.JsonNode.Parse(IssueJson)!.AsObject();
        var sent = body is null ? null : System.Text.Json.Nodes.JsonNode.Parse(body)?.AsObject();
        if (sent != null)
        {
            foreach (var (key, value) in sent)
            {
                if (key.StartsWith("clear", StringComparison.Ordinal)) { if (key == "clearDueDate") issue["dueDate"] = null; if (key == "clearStoryPoints") issue["storyPoints"] = null; if (key == "clearAssignee") { issue["assigneeUserId"] = null; issue["assigneeUserIds"] = new System.Text.Json.Nodes.JsonArray(); } continue; }
                issue[key] = value?.DeepClone();
            }
        }
        issue["version"] = 4;
        return issue.ToJsonString();
    }

    public IEnumerable<(HttpRequestMessage Request, string? Body)> Puts => Handler.Calls.Where(c => c.Request.Method == HttpMethod.Put);

    public T Tool<T>() where T : class => (T)Activator.CreateInstance(typeof(T), new FakeHttpClientFactory(Handler), new HttpContextAccessor())!;
}

public class AssignIssueToolTests
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs)
    {
        var args = new Dictionary<string, object?> { ["issueIdOrKey"] = "FLP-7", [IssueVisibility.CallerUserIdArgument] = 5L, [IssueVisibility.CallerRolesArgument] = "ADMIN" };
        foreach (var (key, value) in pairs) args[key] = value;
        return args;
    }

    private static JsonElement Data(ToolResult result) => JsonDocument.Parse(result.Data!.ToString()!).RootElement;

    [Fact]
    public async Task AssignsToAPersonByName_AndSendsTheIssuesVersion()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Ravi")));

        Assert.True(result.Success, result.Error);
        var put = Assert.Single(jira.Puts);
        Assert.Equal("/api/issues/42/assignee", put.Request.RequestUri!.AbsolutePath);
        Assert.Equal("\"3\"", put.Request.Headers.IfMatch.Single().Tag);
        Assert.Equal(8, JsonDocument.Parse(put.Body!).RootElement.GetProperty("assigneeUserId").GetInt64());

        var data = Data(result);
        Assert.True(data.GetProperty("assigned").GetBoolean());
        Assert.Equal("FLP-7", data.GetProperty("issueKey").GetString());
        Assert.Equal("Asha Kumar", data.GetProperty("previousAssignees").GetString());
        Assert.Equal("Ravi Menon", data.GetProperty("assignees").GetString());
    }

    [Fact]
    public async Task AFirstNameThatFitsTwoPeople_IsRefusedWithBothNames_AndNothingIsChanged()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Priya")));

        Assert.False(result.Success);
        Assert.Contains("Priya Nair (userId 6)", result.Error);
        Assert.Contains("Priya Shah (userId 7)", result.Error);
        Assert.Contains("Ask the user which one", result.Error);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task AFullNameSettlesTheAmbiguity()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Priya Shah")));

        Assert.True(result.Success, result.Error);
        Assert.Equal(7, JsonDocument.Parse(jira.PutBody!).RootElement.GetProperty("assigneeUserId").GetInt64());
    }

    [Fact]
    public async Task ANameNobodyHas_ListsWhoCouldBeAssigned()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Zoe")));

        Assert.False(result.Success);
        Assert.Contains("No one on this project is called 'Zoe'", result.Error);
        Assert.Contains("Ravi Menon", result.Error);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task OnlyPeopleOnTheIssuesOwnProjectCanBeChosen()
    {
        var jira = new FakeJira { Members = [5, 6] };

        
        var byName = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Ravi")));
        var byId = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "8")));

        Assert.False(byName.Success);
        Assert.False(byId.Success);
        Assert.Contains("not a member of this issue's project", byId.Error);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task MeMeansTheSignedInPerson_NotWhatTheModelSays()
    {
        var jira = new FakeJira { IssueJson = jiraIssue(assignee: 8) };
        var args = Args(("assignee", "me"));
        args[IssueVisibility.CallerUserIdArgument] = 6L;

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
        Assert.Equal(6, JsonDocument.Parse(jira.PutBody!).RootElement.GetProperty("assigneeUserId").GetInt64());
    }

    [Fact]
    public async Task AddToExisting_KeepsTheCurrentAssignees()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Ravi"), ("addToExisting", true)));

        Assert.True(result.Success, result.Error);
        var sent = JsonDocument.Parse(jira.PutBody!).RootElement;
        Assert.Equal([5L, 8L], sent.GetProperty("assigneeUserIds").EnumerateArray().Select(x => x.GetInt64()));
        Assert.Equal("Asha Kumar, Ravi Menon", Data(result).GetProperty("assignees").GetString());
    }

    [Fact]
    public async Task AssigningToTheCurrentAssigneeChangesNothing()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Asha")));

        Assert.False(result.Success);
        Assert.Contains("already assigned", result.Error);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task UnassignClearsTheAssignee()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("unassign", true)));

        Assert.True(result.Success, result.Error);
        Assert.True(JsonDocument.Parse(jira.PutBody!).RootElement.GetProperty("clearAssignee").GetBoolean());
        Assert.Equal("nobody", Data(result).GetProperty("assignees").GetString());
    }

    [Fact]
    public async Task UnassigningAnIssueWithNoAssignee_SaysSo()
    {
        var jira = new FakeJira { IssueJson = jiraIssue(assignee: null) };

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("unassign", true)));

        Assert.False(result.Success);
        Assert.Contains("no assignee", result.Error);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task NamingSomeoneAndUnassigningAtOnce_IsRefused()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Ravi"), ("unassign", true)));

        Assert.False(result.Success);
        Assert.Empty(jira.Handler.Calls);
    }

    [Fact]
    public async Task WithoutAnAssignee_TheToolAsksWho()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args());

        Assert.False(result.Success);
        Assert.Contains("Who should it be assigned to", result.Error);
        Assert.Empty(jira.Handler.Calls);
    }

    [Fact]
    public async Task SomeoneWhoSeesOnlyTheirOwnIssues_CannotReassignSomeoneElsesIssue()
    {
        var jira = new FakeJira();
        var args = Args(("assignee", "Ravi"));
        args[IssueVisibility.OwnerArgument] = 6L;   

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("not assigned to you", result.Error);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task WhenTheServiceRefuses_ThePersonIsToldWhy()
    {
        var jira = new FakeJira { PutStatus = HttpStatusCode.Forbidden };

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(Args(("assignee", "Ravi")));

        Assert.False(result.Success);
        Assert.Contains("was not reassigned", result.Error);
        Assert.Contains("Not allowed by the workflow.", result.Error);
    }

    [Fact]
    public async Task AnIssueThatDoesNotExist_IsReportedAsNotFound()
    {
        var jira = new FakeJira();
        var args = Args(("assignee", "Ravi"));
        args["issueIdOrKey"] = "9999";
        jira.IssueId = 1;

        var result = await jira.Tool<AssignIssueTool>().ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("9999", result.Error);
    }

    [Theory]
    [InlineData("asha@x.test", 5)]
    [InlineData("ASHA KUMAR", 5)]
    [InlineData("kum", 5)]
    [InlineData("Menon", 8)]
    [InlineData("ravi m", 8)]
    [InlineData("12", 12)]
    public void NamesAreMatchedTheWayPeopleTypeThem(string said, long expected)
    {
        var members = new[] { new AssigneeCandidate(5, "Asha Kumar", "asha@x.test"), new AssigneeCandidate(8, "Ravi Menon", "ravi@x.test") };
        var (id, problem) = AssignIssueTool.Resolve(said, 5, members);
        Assert.Null(problem);
        Assert.Equal(expected, id);
    }

    [Fact]
    public void WhenNamesCannotBeLookedUp_OnlyAnIdWorks()
    {
        var members = new[] { new AssigneeCandidate(5, null, null) };
        var (id, problem) = AssignIssueTool.Resolve("Asha", 5, members);
        Assert.Null(id);
        Assert.Contains("list_project_members", problem);
    }

    private static string jiraIssue(long? assignee) =>
        $$"""{"id":42,"issueKey":"FLP-7","summary":"Fix login","statusCode":"TODO","priority":"MEDIUM","projectId":9,"assigneeUserId":{{(assignee?.ToString() ?? "null")}},"assigneeUserIds":[{{(assignee?.ToString() ?? "")}}],"version":3}""";
}

public class UpdateIssueToolTests
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs)
    {
        var args = new Dictionary<string, object?> { ["issueIdOrKey"] = "FLP-7", [IssueVisibility.CallerUserIdArgument] = 5L, [IssueVisibility.CallerRolesArgument] = "ADMIN" };
        foreach (var (key, value) in pairs) args[key] = value;
        return args;
    }

    private static JsonElement Data(ToolResult result) => JsonDocument.Parse(result.Data!.ToString()!).RootElement;
    private static string Tomorrow => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(1).ToString("yyyy-MM-dd");

    [Fact]
    public async Task ChangesOnlyWhatWasAskedFor_AndReportsEachChange()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("summary", "Fix SSO login"), ("priority", "high")));

        Assert.True(result.Success, result.Error);
        var put = Assert.Single(jira.Puts);
        Assert.Equal("/api/issues/42", put.Request.RequestUri!.AbsolutePath);
        Assert.Equal("\"3\"", put.Request.Headers.IfMatch.Single().Tag);
        var sent = JsonDocument.Parse(put.Body!).RootElement;
        Assert.Equal(["priority", "summary"], sent.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal("HIGH", sent.GetProperty("priority").GetString());

        var data = Data(result);
        Assert.True(data.GetProperty("updated").GetBoolean());
        Assert.Equal("Fix SSO login", data.GetProperty("summary").GetString());
        var changes = data.GetProperty("changes").EnumerateArray().ToList();
        Assert.Contains(changes, c => c.GetProperty("field").GetString() == "priority" && c.GetProperty("from").GetString() == "MEDIUM" && c.GetProperty("to").GetString() == "HIGH");
    }

    [Fact]
    public async Task TheOldVocabularyForTheTopPriorityIsUnderstood()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("priority", "critical")));

        Assert.True(result.Success, result.Error);
        Assert.Equal("HIGHEST", JsonDocument.Parse(jira.PutBody!).RootElement.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task SetsADueDateAndStoryPoints()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("dueDate", Tomorrow), ("storyPoints", 5)));

        Assert.True(result.Success, result.Error);
        var sent = JsonDocument.Parse(jira.PutBody!).RootElement;
        Assert.Equal(Tomorrow, sent.GetProperty("dueDate").GetString());
        Assert.Equal(5, sent.GetProperty("storyPoints").GetInt32());
    }

    [Fact]
    public async Task NothingToChange_AsksWhatToChange_WithoutCallingAnyService()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args());

        Assert.False(result.Success);
        Assert.Contains("Nothing to change", result.Error);
        Assert.Empty(jira.Handler.Calls);
    }

    [Theory]
    [InlineData("dueDate", "2020-01-01", "in the past")]
    [InlineData("dueDate", "next friday", "YYYY-MM-DD")]
    [InlineData("storyPoints", 4, "not a valid estimate")]
    [InlineData("priority", "urgent", "priority must be one of")]
    public async Task BadValuesAreRefusedBeforeAnythingIsSent(string field, object value, string expected)
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args((field, value)));

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error);
        Assert.Empty(jira.Handler.Calls);
    }

    [Fact]
    public async Task AppendingKeepsTheOldDescription()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("appendToDescription", "Also fails on Safari.")));

        Assert.True(result.Success, result.Error);
        Assert.Equal("Users cannot sign in.\n\nAlso fails on Safari.", JsonDocument.Parse(jira.PutBody!).RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task ReplacingAndAppendingTogetherIsRefused()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("description", "New"), ("appendToDescription", "More")));

        Assert.False(result.Success);
        Assert.Empty(jira.Handler.Calls);
    }

    [Fact]
    public async Task ClearingTheDueDateAndEstimate_IsExplicit()
    {
        var jira = new FakeJira { IssueJson = """{"id":42,"issueKey":"FLP-7","summary":"Fix login","statusCode":"TODO","priority":"MEDIUM","projectId":9,"assigneeUserId":5,"assigneeUserIds":[5],"dueDate":"2030-01-01","storyPoints":3,"version":3}""" };

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("clearDueDate", true), ("clearStoryPoints", true)));

        Assert.True(result.Success, result.Error);
        var sent = JsonDocument.Parse(jira.PutBody!).RootElement;
        Assert.True(sent.GetProperty("clearDueDate").GetBoolean());
        Assert.True(sent.GetProperty("clearStoryPoints").GetBoolean());
        Assert.False(sent.TryGetProperty("dueDate", out _));
    }

    [Fact]
    public async Task AskingForWhatIsAlreadyThere_ChangesNothing()
    {
        var jira = new FakeJira();

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("summary", "Fix login"), ("priority", "MEDIUM")));

        Assert.False(result.Success);
        Assert.Contains("already looks like that", result.Error);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task SomeoneWhoSeesOnlyTheirOwnIssues_CannotEditSomeoneElsesIssue()
    {
        var jira = new FakeJira();
        var args = Args(("summary", "Hijacked"));
        args[IssueVisibility.OwnerArgument] = 6L;

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Empty(jira.Puts);
    }

    [Fact]
    public async Task SomeoneWhoSeesOnlyTheirOwnIssues_CanEditTheirOwn()
    {
        var jira = new FakeJira();
        var args = Args(("summary", "Fix SSO login"));
        args[IssueVisibility.OwnerArgument] = 5L;

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task WhenTheServiceRefuses_ThePersonIsToldWhy()
    {
        var jira = new FakeJira { PutStatus = HttpStatusCode.Conflict };

        var result = await jira.Tool<UpdateIssueTool>().ExecuteAsync(Args(("summary", "New title")));

        Assert.False(result.Success);
        Assert.Contains("was not updated", result.Error);
    }
}

public class WorkDigestToolTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    private static string Issue(long id, string key, string priority, DateOnly? due, string status = "TODO") =>
        $"{{\"id\":{id},\"issueKey\":\"{key}\",\"summary\":\"Task {key}\",\"issueType\":\"TASK\",\"statusCode\":\"{status}\",\"priority\":\"{priority}\",\"dueDate\":{(due is null ? "null" : $"\"{due:yyyy-MM-dd}\"")},\"storyPoints\":3,\"remainingEstimateMinutes\":120}}";

    private static string Page(params string[] issues) => $"{{\"items\":[{string.Join(",", issues)}],\"totalCount\":{issues.Length}}}";

    private static (RecordingHandler Handler, GetWorkDigestTool Tool) Make(Func<string, string?> pageFor, HttpStatusCode? failGroup = null)
    {
        var handler = new RecordingHandler(request =>
        {
            var query = request.RequestUri!.Query;
            if (request.RequestUri.AbsolutePath == "/api/employees") return Http.Json("""{"items":[{"id":1,"userId":5,"firstName":"Asha","lastName":"Kumar"}],"totalPages":1}""");
            var group = System.Web.HttpUtility.ParseQueryString(query)["WorkGroup"]!;
            var body = pageFor(group);
            return body is null ? Http.Json("{}", failGroup ?? HttpStatusCode.InternalServerError) : Http.Json(body);
        });
        return (handler, new GetWorkDigestTool(new FakeHttpClientFactory(handler), new HttpContextAccessor()));
    }

    private static Dictionary<string, object?> Me(long user = 5, long? owner = null, params (string, object?)[] more)
    {
        var args = new Dictionary<string, object?> { [IssueVisibility.CallerUserIdArgument] = user, [IssueVisibility.CallerRolesArgument] = "DEVELOPER" };
        if (owner.HasValue) args[IssueVisibility.OwnerArgument] = owner.Value;
        foreach (var (key, value) in more) args[key] = value;
        return args;
    }

    private static JsonElement Data(ToolResult result) => JsonDocument.Parse(result.Data!.ToString()!).RootElement;

    [Fact]
    public async Task AsksTheWorkspaceForEachGroupItAlreadyUses_ForThePersonAndTheirToday()
    {
        var (handler, tool) = Make(_ => Page());

        await tool.ExecuteAsync(Me());

        var searches = handler.Calls.Where(c => c.Request.RequestUri!.AbsolutePath == "/api/issues/search").Select(c => System.Web.HttpUtility.ParseQueryString(c.Request.RequestUri!.Query)).ToList();
        Assert.Equal(["DUE_SOON", "IN_PROGRESS", "OTHER", "OVERDUE"], searches.Select(q => q["WorkGroup"]!).OrderBy(g => g));
        Assert.All(searches, q => { Assert.Equal("5", q["AssigneeUserId"]); Assert.Equal($"{Today:yyyy-MM-dd}", q["Today"]); });
    }

    [Fact]
    public async Task ListsTheMostPressingFirst_WithAReasonForEach()
    {
        var (_, tool) = Make(group => group switch
        {
            "OVERDUE" => Page(Issue(1, "FLP-1", "LOW", Today.AddDays(-9)), Issue(2, "FLP-2", "HIGHEST", Today.AddDays(-2))),
            "DUE_SOON" => Page(Issue(3, "FLP-3", "HIGH", Today.AddDays(4)), Issue(4, "FLP-4", "LOW", Today)),
            "IN_PROGRESS" => Page(Issue(5, "FLP-5", "MEDIUM", null, "IN_PROGRESS")),
            _ => Page(Issue(6, "FLP-6", "LOW", null))
        });

        var result = await tool.ExecuteAsync(Me());

        Assert.True(result.Success, result.Error);
        var data = Data(result);
        var items = data.GetProperty("items").EnumerateArray().ToList();

        
        Assert.Equal(["FLP-2", "FLP-1", "FLP-4", "FLP-3", "FLP-5", "FLP-6"], items.Select(i => i.GetProperty("issueKey").GetString()));
        Assert.Equal("overdue by 2 days, highest priority", items[0].GetProperty("why").GetString());
        Assert.Equal("due today, low priority", items[2].GetProperty("why").GetString());
        Assert.Equal("due in 4 days, high priority", items[3].GetProperty("why").GetString());
        Assert.Equal("already under way (in progress), medium priority", items[4].GetProperty("why").GetString());
        Assert.Equal("not started, no deadline, low priority", items[5].GetProperty("why").GetString());
        Assert.Equal(2.0, items[0].GetProperty("remainingHours").GetDouble());

        var counts = data.GetProperty("counts");
        Assert.Equal(2, counts.GetProperty("overdue").GetInt32());
        Assert.Equal(6, counts.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task LongListsAreCut_AndTheTotalIsStillTold()
    {
        var many = Enumerable.Range(1, 12).Select(i => Issue(i, $"FLP-{i}", "MEDIUM", Today.AddDays(-i))).ToArray();
        var (_, tool) = Make(group => group == "OVERDUE" ? Page(many) : Page());

        var data = Data(await tool.ExecuteAsync(Me()));

        Assert.Equal(5, data.GetProperty("items").GetArrayLength());
        Assert.Equal(12, data.GetProperty("counts").GetProperty("overdue").GetInt32());
        Assert.Contains("Showing the top 5 of 12", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task NothingAssigned_SaysSo()
    {
        var (_, tool) = Make(_ => Page());

        var data = Data(await tool.ExecuteAsync(Me()));

        Assert.Equal(0, data.GetProperty("items").GetArrayLength());
        Assert.Contains("Nothing open", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task SomeoneWhoSeesOnlyTheirOwnIssues_CannotAskForAnotherPersonsWork()
    {
        var (handler, tool) = Make(_ => Page());

        var result = await tool.ExecuteAsync(Me(owner: 5, more: ("userId", 9L)));

        Assert.False(result.Success);
        Assert.Contains("only see your own", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task AManagerMayLookAtSomeoneElse_AndTheirNameIsShown()
    {
        var (handler, tool) = Make(_ => Page());

        var result = await tool.ExecuteAsync(Me(user: 1, more: ("userId", 5L)));

        Assert.True(result.Success, result.Error);
        Assert.All(handler.Calls.Where(c => c.Request.RequestUri!.AbsolutePath == "/api/issues/search"),
            c => Assert.Equal("5", System.Web.HttpUtility.ParseQueryString(c.Request.RequestUri!.Query)["AssigneeUserId"]));
        Assert.Equal("Asha Kumar", Data(result).GetProperty("forName").GetString());
    }

    [Fact]
    public async Task WhenOneGroupCannotBeLoaded_ItDoesNotPretendTheListIsComplete()
    {
        var (_, tool) = Make(group => group == "DUE_SOON" ? null : Page());

        var result = await tool.ExecuteAsync(Me());

        Assert.False(result.Success);
        Assert.Contains("Could not load the work list", result.Error);
    }

    [Fact]
    public async Task WithoutKnowingWho_ItDoesNotGuess()
    {
        var (handler, tool) = Make(_ => Page());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public void ItIsALookup_SoTheAuditLogAndTheInjectionGuardTreatItLikeOne() =>
        Assert.Equal(ToolActivityKind.Read, ToolActivity.Classify("get_work_digest"));

    [Fact]
    public void TheWritingToolsAreClassifiedAsWrites()
    {
        Assert.Equal(ToolActivityKind.Write, ToolActivity.Classify("assign_issue"));
        Assert.Equal(ToolActivityKind.Write, ToolActivity.Classify("update_issue"));
    }
}

using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;

public class PrepareWorkLogTests
{
    private static Dictionary<string, object?> Args(string json, long? caller = 7, long? owner = null)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        if (caller.HasValue) args[IssueVisibility.CallerUserIdArgument] = caller.Value;
        if (owner.HasValue) args[IssueVisibility.OwnerArgument] = owner.Value;
        return args;
    }

    private const string Mine =
        "{\"items\":[" +
        "{\"id\":42,\"issueKey\":\"FLP360-42\",\"summary\":\"Supervisor agent\",\"statusCode\":\"IN_PROGRESS\",\"priority\":\"HIGH\",\"assigneeUserId\":7}," +
        "{\"id\":43,\"issueKey\":\"FLP360-43\",\"summary\":\"Finished thing\",\"statusCode\":\"DONE\",\"priority\":\"LOW\",\"assigneeUserId\":7}," +
        "{\"id\":44,\"issueKey\":\"FLP360-44\",\"summary\":\"Someone else's\",\"statusCode\":\"TODO\",\"priority\":\"LOW\",\"assigneeUserId\":9}]," +
        "\"totalCount\":3}";

    private static JsonDocument Data(ToolResult result)
    {
        Assert.True(result.Success, result.Error);
        return JsonDocument.Parse((string)result.Data!);
    }

    [Fact]
    public async Task OffersOnlyMyOpenIssues_AsAForm()
    {
        var (handler, factory) = new Routes().Get("/api/issues/search", Mine).Build();

        var result = await new PrepareWorkLogTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{}"));

        using var doc = Data(result);
        Assert.True(doc.RootElement.GetProperty("workLogForm").GetBoolean());
        var keys = doc.RootElement.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("issueKey").GetString()).ToArray();
        Assert.Equal(new[] { "FLP360-42" }, keys);
        Assert.Contains("AssigneeUserId=7", Assert.Single(handler.Calls).Request.RequestUri!.Query);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", doc.RootElement.GetProperty("today").GetString());
    }

    [Fact]
    public async Task PreselectsAnIssueTheUserNamed_EvenWhenItIsNotInTheOpenList()
    {
        var (_, factory) = new Routes()
            .Get("/api/issues/search", Mine)
            .Get("/api/issues/43", "{\"id\":43,\"issueKey\":\"FLP360-43\",\"summary\":\"Finished thing\",\"statusCode\":\"DONE\",\"assigneeUserId\":7}")
            .Build();

        var result = await new PrepareWorkLogTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{\"issueIdOrKey\":\"43\"}", owner: 7));

        using var doc = Data(result);
        Assert.Equal(43, doc.RootElement.GetProperty("selectedIssueId").GetInt64());
        Assert.Equal("FLP360-43", doc.RootElement.GetProperty("issues")[0].GetProperty("issueKey").GetString());
    }

    [Fact]
    public async Task RefusesToPreselectAnIssueAssignedToSomeoneElse_ForADeveloper()
    {
        var (_, factory) = new Routes()
            .Get("/api/issues/search", Mine)
            .Get("/api/issues/44", "{\"id\":44,\"issueKey\":\"FLP360-44\",\"summary\":\"x\",\"statusCode\":\"TODO\",\"assigneeUserId\":9}")
            .Build();

        var result = await new PrepareWorkLogTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{\"issueIdOrKey\":\"44\"}", owner: 7));

        Assert.False(result.Success);
        Assert.Contains("not assigned to you", result.Error);
    }

    [Fact]
    public async Task WithNoOpenIssues_SaysSoInsteadOfShowingAnEmptyList()
    {
        var (_, factory) = new Routes().Get("/api/issues/search", "{\"items\":[],\"totalCount\":0}").Build();

        var result = await new PrepareWorkLogTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{}"));

        using var doc = Data(result);
        Assert.Equal(0, doc.RootElement.GetProperty("issues").GetArrayLength());
        Assert.Contains("no open issues", doc.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public async Task NeedsToKnowWhoIsAsking()
    {
        var (handler, factory) = new Routes().Build();

        var result = await new PrepareWorkLogTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{}", caller: null));

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public void IsOfferedOnlyToPeopleWhoMayLogWork() =>
        Assert.Equal("WORKLOG_CREATE", new PrepareWorkLogTool(new FakeHttpClientFactory(new RecordingHandler(_ => Http.Json("{}"))), new HttpContextAccessor()).RequiredPermission);
}

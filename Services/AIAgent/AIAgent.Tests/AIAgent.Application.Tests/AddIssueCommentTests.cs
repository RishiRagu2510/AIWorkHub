using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;

public class AddIssueCommentTests
{
    private static Dictionary<string, object?> Args(string json, long? caller = 7, long? owner = null)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        if (caller.HasValue) args[IssueVisibility.CallerUserIdArgument] = caller.Value;
        if (owner.HasValue) args[IssueVisibility.OwnerArgument] = owner.Value;
        return args;
    }

    private const string Issue = "{\"id\":17,\"issueKey\":\"FLP360-17\",\"summary\":\"Login broken\",\"reporterUserId\":9,\"assigneeUserId\":7,\"assigneeUserIds\":[7]}";

    private static JsonDocument Data(ToolResult result)
    {
        Assert.True(result.Success, result.Error);
        return JsonDocument.Parse((string)result.Data!);
    }

    private static (RecordingHandler, IHttpClientFactory) Build(string issue = Issue) => new Routes()
        .Get("/api/issues/17", issue)
        .Post("/api/issues/17/comments", "{\"id\":501,\"body\":\"x\"}")
        .Build();

    private static async Task<(ToolResult Result, RecordingHandler Handler)> Run(string json, long? owner = null, string issue = Issue)
    {
        var (handler, factory) = Build(issue);
        var result = await new AddIssueCommentTool(factory, new HttpContextAccessor()).ExecuteAsync(Args(json, owner: owner));
        return (result, handler);
    }

    private static JsonDocument PostedBody(RecordingHandler handler)
    {
        var post = handler.Calls.Single(c => c.Request.Method == HttpMethod.Post);
        return JsonDocument.Parse(post.Body!);
    }

    [Fact]
    public async Task PostsTheComment_AndMentionsTheReporterWhenAsked()
    {
        var (result, handler) = await Run("{\"issueIdOrKey\":\"17\",\"comment\":\"  Can you share the steps to reproduce?  \",\"notifyReporter\":true}");

        using var doc = Data(result);
        Assert.True(doc.RootElement.GetProperty("commented").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("reporterMentioned").GetBoolean());
        Assert.Equal(501, doc.RootElement.GetProperty("commentId").GetInt64());
        Assert.False(doc.RootElement.TryGetProperty("issueKey", out _)); 

        using var body = PostedBody(handler);
        Assert.Equal("Can you share the steps to reproduce?", body.RootElement.GetProperty("body").GetString());
        Assert.Equal(new long[] { 9 }, body.RootElement.GetProperty("mentionedUserIds").EnumerateArray().Select(e => e.GetInt64()).ToArray());
    }

    [Fact]
    public async Task WithoutNotifyReporter_NobodyIsMentioned()
    {
        var (result, handler) = await Run("{\"issueIdOrKey\":\"17\",\"comment\":\"Fix deployed.\"}");

        using var doc = Data(result);
        Assert.False(doc.RootElement.GetProperty("reporterMentioned").GetBoolean());
        using var body = PostedBody(handler);
        Assert.Empty(body.RootElement.GetProperty("mentionedUserIds").EnumerateArray());
    }

    [Fact]
    public async Task TheReporterIsNotMentionedToThemselves()
    {
        var (result, handler) = await Run("{\"issueIdOrKey\":\"17\",\"comment\":\"note\",\"notifyReporter\":true}",
            issue: "{\"id\":17,\"issueKey\":\"FLP360-17\",\"summary\":\"s\",\"reporterUserId\":7,\"assigneeUserId\":7}");

        using var doc = Data(result);
        Assert.False(doc.RootElement.GetProperty("reporterMentioned").GetBoolean());
        Assert.Contains("reporter", doc.RootElement.GetProperty("note").GetString());
        using var body = PostedBody(handler);
        Assert.Empty(body.RootElement.GetProperty("mentionedUserIds").EnumerateArray());
    }

    [Fact]
    public async Task AnIssueWithNoReporter_StillGetsTheComment()
    {
        var (result, _) = await Run("{\"issueIdOrKey\":\"17\",\"comment\":\"note\",\"notifyReporter\":true}",
            issue: "{\"id\":17,\"issueKey\":\"FLP360-17\",\"summary\":\"s\",\"assigneeUserId\":7}");

        using var doc = Data(result);
        Assert.Contains("no reporter", doc.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ADeveloperCannotCommentOnSomeoneElsesIssue()
    {
        var (result, handler) = await Run("{\"issueIdOrKey\":\"17\",\"comment\":\"hi\"}", owner: 5);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("{\"comment\":\"hi\"}", "issueIdOrKey")]
    [InlineData("{\"issueIdOrKey\":\"17\"}", "comment")]
    [InlineData("{\"issueIdOrKey\":\"17\",\"comment\":\"   \"}", "comment")]
    public async Task AsksForWhatIsMissing_AndPostsNothing(string json, string missing)
    {
        var (result, handler) = await Run(json);

        Assert.False(result.Success);
        Assert.Contains(missing, result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task RefusesAnOverlongComment()
    {
        var (result, handler) = await Run("{\"issueIdOrKey\":\"17\",\"comment\":\"" + new string('x', 20001) + "\"}");

        Assert.False(result.Success);
        Assert.Contains("too long", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task TheApiRefusal_IsReportedAndNotPretendedAway()
    {
        var (handler, factory) = new Routes()
            .Get("/api/issues/17", Issue)
            .Post("/api/issues/17/comments", "{\"message\":\"You do not have permission to do that.\"}", System.Net.HttpStatusCode.Forbidden)
            .Build();

        var result = await new AddIssueCommentTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{\"issueIdOrKey\":\"17\",\"comment\":\"hi\"}"));

        Assert.False(result.Success);
        Assert.Contains("was not posted", result.Error);
        Assert.Contains("permission", result.Error);
        Assert.NotEmpty(handler.Calls);
    }
}

using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Infrastructure.Identity;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIAgent.Application.Tests;


public class IssueVisibilityToolTests
{
    private const long Me = 7;
    private const long Other = 9;

    private static (RecordingHandler Handler, FakeHttpClientFactory Factory) Setup(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        return (handler, new FakeHttpClientFactory(handler));
    }

    private static Dictionary<string, object?> Args(string json, long? owner = Me)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        if (owner.HasValue) args[IssueVisibility.OwnerArgument] = owner.Value;
        return args;
    }

    private static string Issue(long id, long assignee, long[]? also = null) =>
        $"{{\"id\":{id},\"issueKey\":\"FLP-{id}\",\"assigneeUserId\":{assignee},\"assigneeUserIds\":[{string.Join(",", also ?? [assignee])}],\"version\":1}}";

    private static SearchIssuesTool Search(FakeHttpClientFactory f) => new(f, new HttpContextAccessor());
    private static GetIssueDetailsTool Details(FakeHttpClientFactory f) => new(f, new HttpContextAccessor());
    private static TransitionIssueTool Transition(FakeHttpClientFactory f) => new(f, new HttpContextAccessor());

    

    [Fact]
    public async Task Search_AsksJiraForOnlyTheCallersIssues_EvenWhenTheModelNamedNoOne()
    {
        var (handler, factory) = Setup(_ => Http.Json("{\"items\":[],\"totalCount\":0}"));

        await Search(factory).ExecuteAsync(Args("{\"query\":\"login\"}"));

        Assert.Contains($"AssigneeUserId={Me}", handler.Calls.Single().Request.RequestUri!.Query);
    }

    [Fact]
    public async Task Search_RefusesAnotherPersonsIssues_WithoutCallingJira()
    {
        var (handler, factory) = Setup(_ => Http.Json("{}"));

        var result = await Search(factory).ExecuteAsync(Args($"{{\"assigneeUserId\":{Other}}}"));

        Assert.False(result.Success);
        Assert.Contains("only view issues assigned to you", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Search_NamingThemselves_IsFine()
    {
        var (handler, factory) = Setup(_ => Http.Json("{\"items\":[],\"totalCount\":0}"));

        var result = await Search(factory).ExecuteAsync(Args($"{{\"assigneeUserId\":{Me}}}"));

        Assert.True(result.Success, result.Error);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Search_DropsAnythingJiraReturnsThatIsNotTheirs()
    {
        var body = $"{{\"items\":[{Issue(1, Me)},{Issue(2, Other)},{Issue(3, Other, [Other, Me])}],\"totalCount\":3}}";
        var (_, factory) = Setup(_ => Http.Json(body));

        var result = await Search(factory).ExecuteAsync(Args("{}"));

        Assert.True(result.Success, result.Error);
        using var doc = JsonDocument.Parse((string)result.Data!);
        var ids = doc.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()).ToArray();
        Assert.Equal([1L, 3L], ids);
        Assert.Equal(2, doc.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Search_ForAnAdminOrManager_IsNotNarrowed()
    {
        var body = $"{{\"items\":[{Issue(1, Me)},{Issue(2, Other)}],\"totalCount\":2}}";
        var (handler, factory) = Setup(_ => Http.Json(body));

        var result = await Search(factory).ExecuteAsync(Args("{}", owner: null));

        Assert.DoesNotContain("AssigneeUserId", handler.Calls.Single().Request.RequestUri!.Query);
        using var doc = JsonDocument.Parse((string)result.Data!);
        Assert.Equal(2, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Search_WhenTheCallerCannotBeIdentified_ShowsNothing()
    {
        var (handler, factory) = Setup(_ => Http.Json("{}"));
        var args = Args("{}", owner: null);
        args[IssueVisibility.UnknownCallerArgument] = true;

        var result = await Search(factory).ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    

    [Fact]
    public async Task Details_OfSomeoneElsesIssue_LooksLikeItDoesNotExist()
    {
        var (_, factory) = Setup(_ => Http.Json(Issue(2, Other)));

        var result = await Details(factory).ExecuteAsync(Args("{\"issueKey\":\"FLP-2\"}"));

        Assert.False(result.Success);
        Assert.Contains("not assigned to you", result.Error);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(Me, null)]
    [InlineData(Other, new[] { Other, Me })]
    public async Task Details_OfTheirOwnIssue_IsShown(long primaryAssignee, long[]? all)
    {
        var (_, factory) = Setup(_ => Http.Json(Issue(5, primaryAssignee, all)));

        var result = await Details(factory).ExecuteAsync(Args("{\"issueKey\":\"FLP-5\"}"));

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Details_NumericFallbackPath_IsCheckedToo()
    {
        
        var (_, factory) = Setup(r => r.RequestUri!.AbsolutePath.StartsWith("/api/issues/key/")
            ? Http.Json("{}", HttpStatusCode.NotFound)
            : Http.Json(Issue(2, Other)));

        var result = await Details(factory).ExecuteAsync(Args("{\"issueKey\":\"FLP-2\"}"));

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Details_ForAnAdminOrManager_ShowsAnyIssue()
    {
        var (_, factory) = Setup(_ => Http.Json(Issue(2, Other)));

        var result = await Details(factory).ExecuteAsync(Args("{\"issueKey\":\"FLP-2\"}", owner: null));

        Assert.True(result.Success, result.Error);
    }

    

    private static Func<HttpRequestMessage, HttpResponseMessage> JiraWithIssue(string issueJson) => r =>
    {
        if (r.Method == HttpMethod.Put) return Http.Json("{\"ok\":true}");
        return Http.Json(issueJson);
    };

    [Fact]
    public async Task Transition_OfSomeoneElsesIssue_IsRefusedBeforeAnythingChanges()
    {
        var (handler, factory) = Setup(JiraWithIssue(Issue(2, Other)));

        var result = await Transition(factory).ExecuteAsync(Args("{\"issueIdOrKey\":\"2\",\"statusCode\":\"DONE\"}"));

        Assert.False(result.Success);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task Transition_OfTheirOwnIssue_Goes_Ahead()
    {
        var (handler, factory) = Setup(JiraWithIssue(Issue(2, Me)));

        var result = await Transition(factory).ExecuteAsync(Args("{\"issueIdOrKey\":\"2\",\"statusCode\":\"DONE\"}"));

        Assert.True(result.Success, result.Error);
        Assert.Contains(handler.Calls, c => c.Request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task Transition_ForAnAdminOrManager_WorksOnAnyIssue()
    {
        var (handler, factory) = Setup(JiraWithIssue(Issue(2, Other)));

        var result = await Transition(factory).ExecuteAsync(Args("{\"issueIdOrKey\":\"2\",\"statusCode\":\"DONE\"}", owner: null));

        Assert.True(result.Success, result.Error);
        Assert.Contains(handler.Calls, c => c.Request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task Transition_WhenTheIssueCannotBeReadToCheckOwnership_IsRefused()
    {
        var (handler, factory) = Setup(r => r.Method == HttpMethod.Put ? Http.Json("{}") : Http.Json("{}", HttpStatusCode.Forbidden));

        var result = await Transition(factory).ExecuteAsync(Args("{\"issueIdOrKey\":\"2\",\"statusCode\":\"DONE\"}"));

        Assert.False(result.Success);
        Assert.DoesNotContain(handler.Calls, c => c.Request.Method == HttpMethod.Put);
    }
}


public class IssueVisibilityOrchestratorTests
{
    private sealed class CapturingTool : IAgentTool
    {
        public string Name => "search_issues";
        public string Description => "x";
        public string Category => "Test";
        public string RequiredPermission => "ISSUE_VIEW";
        public object ParameterSchema => new { };
        public Dictionary<string, object?>? Seen { get; private set; }
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Seen = arguments;
            return Task.FromResult(new ToolResult(true, "{}"));
        }
    }

    private sealed class FakeIdentity(VerifiedCaller? caller, bool hasCredentials = true) : ICallerIdentityProvider
    {
        public bool HasCredentials => hasCredentials;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default) => Task.FromResult(caller);
    }

    private static async Task<(AgentChatResponse Response, CapturingTool Tool, List<string> SystemPrompts)> Run(
        Dictionary<string, object>? context,
        ICallerIdentityProvider? identity = null,
        string modelArguments = "{}")
    {
        var tool = new CapturingTool();
        var prompts = new List<string>();
        var step = 0;
        var llm = new MockLlmProvider
        {
            OnChat = (msgs, _) =>
            {
                prompts.AddRange(msgs.Where(m => m.Role == AIAgent.Domain.Models.MessageRole.System).Select(m => m.Content));
                return ++step == 1
                    ? new LlmChatResponse { ToolCalls = [new ToolCall { Id = "1", Name = "search_issues", ArgumentsJson = modelArguments }] }
                    : new LlmChatResponse { Content = "done" };
            }
        };

        var response = await new AthennaAgentOrchestrator(llm, [tool], callerIdentity: identity)
            .ExecuteChatAsync(new AgentChatRequest("show me issues", ExtraContext: context));
        return (response, tool, prompts);
    }

    private static Dictionary<string, object> Context(string role, long? userId = null, params string[] permissions)
    {
        var context = new Dictionary<string, object> { ["role"] = role, ["permissions"] = permissions };
        if (userId.HasValue) context["userId"] = userId.Value;
        return context;
    }

    [Fact]
    public async Task Developer_IsScopedToTheirOwnUserId()
    {
        var (_, tool, _) = await Run(Context("DEVELOPER", 7, "ISSUE_VIEW"));

        Assert.Equal(7L, Convert.ToInt64(tool.Seen![IssueVisibility.OwnerArgument]));
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("MANAGER")]
    [InlineData("PROJECT_MANAGER")]
    [InlineData("ENGINEERING_MANAGER")]
    public async Task AdminsAndManagers_AreNotScoped(string role)
    {
        var (_, tool, _) = await Run(Context(role, 1, "ISSUE_VIEW"));

        Assert.DoesNotContain(IssueVisibility.OwnerArgument, tool.Seen!.Keys);
        Assert.DoesNotContain(IssueVisibility.UnknownCallerArgument, tool.Seen.Keys);
    }

    [Theory]
    [InlineData("HR")]
    [InlineData("DEVELOPER")]
    [InlineData("SOMETHING_ELSE")]
    public async Task EveryoneElse_IsScoped(string role)
    {
        var (_, tool, _) = await Run(Context(role, 7, "ISSUE_VIEW"));

        Assert.Equal(7L, Convert.ToInt64(tool.Seen![IssueVisibility.OwnerArgument]));
    }

    [Fact]
    public async Task DeveloperWhoseIdIsUnknown_IsMarkedUnknownSoNothingIsShown()
    {
        var (_, tool, _) = await Run(Context("DEVELOPER", null, "ISSUE_VIEW"));

        Assert.Equal(true, tool.Seen![IssueVisibility.UnknownCallerArgument]);
        Assert.DoesNotContain(IssueVisibility.OwnerArgument, tool.Seen.Keys);
    }

    [Fact]
    public async Task TheModel_CannotWidenOrRedirectTheScope()
    {
        
        var forged = "{\"__visibleToUserId\":9,\"__unknownCaller\":false,\"query\":\"x\"}";

        var (_, tool, _) = await Run(Context("DEVELOPER", 7, "ISSUE_VIEW"), modelArguments: forged);

        Assert.Equal(7L, Convert.ToInt64(tool.Seen![IssueVisibility.OwnerArgument]));
        Assert.DoesNotContain(IssueVisibility.UnknownCallerArgument, tool.Seen.Keys);
    }

    [Fact]
    public async Task TheModel_CannotScopeAnAdminDownOrFakeTheirIdentity()
    {
        var (_, tool, _) = await Run(Context("ADMIN", 1, "ISSUE_VIEW"), modelArguments: "{\"__visibleToUserId\":9}");

        Assert.DoesNotContain(IssueVisibility.OwnerArgument, tool.Seen!.Keys);
    }

    [Fact]
    public async Task ReservedArguments_AreNotShownToTheUser()
    {
        var (response, _, _) = await Run(Context("DEVELOPER", 7, "ISSUE_VIEW"));

        var shown = Assert.Single(response.ExecutedActions!).Arguments as Dictionary<string, object?>;
        Assert.NotNull(shown);
        Assert.DoesNotContain(IssueVisibility.OwnerArgument, shown!.Keys);
        Assert.DoesNotContain(IssueVisibility.UnknownCallerArgument, shown.Keys);
    }

    [Fact]
    public async Task Prompt_TellsTheModelWhatThisUserMaySee()
    {
        var (_, _, developerPrompts) = await Run(Context("DEVELOPER", 7, "ISSUE_VIEW"));
        var (_, _, adminPrompts) = await Run(Context("ADMIN", 1, "ISSUE_VIEW"));

        Assert.Contains("ONLY issues assigned to them", developerPrompts[0]);
        Assert.Contains("across the whole workspace", adminPrompts[0]);
    }

    

    [Fact]
    public async Task VerifiedIdentity_OverridesAClaimedAdminRoleAndUserId()
    {
        
        var verified = new VerifiedCaller(7, "DEVELOPER", ["DEVELOPER"], ["ISSUE_VIEW"]);

        var (_, tool, _) = await Run(Context("ADMIN", 1, "ISSUE_VIEW"), new FakeIdentity(verified));

        Assert.Equal(7L, Convert.ToInt64(tool.Seen![IssueVisibility.OwnerArgument]));
    }

    [Fact]
    public async Task VerifiedIdentity_PermissionsReplaceClaimedOnes()
    {
        
        var verified = new VerifiedCaller(7, "DEVELOPER", ["DEVELOPER"], ["TIMESHEET_VIEW"]);

        var (response, tool, _) = await Run(Context("DEVELOPER", 7, "ISSUE_VIEW"), new FakeIdentity(verified));

        Assert.Null(tool.Seen);
        Assert.Contains("RBAC Access Denied", response.ExecutedActions!.Single().Result!.ToString());
    }

    [Fact]
    public async Task UserWithAdminAmongSeveralRoles_IsNotScoped()
    {
        var verified = new VerifiedCaller(1, "DEVELOPER", ["DEVELOPER", "ADMIN"], ["ISSUE_VIEW"]);

        var (_, tool, _) = await Run(null, new FakeIdentity(verified));

        Assert.DoesNotContain(IssueVisibility.OwnerArgument, tool.Seen!.Keys);
    }

    [Fact]
    public async Task TokenThatCannotBeVerified_StopsEverything()
    {
        var calledModel = false;
        var llm = new MockLlmProvider { OnChat = (_, _) => { calledModel = true; return new LlmChatResponse { Content = "x" }; } };

        var response = await new AthennaAgentOrchestrator(llm, [new CapturingTool()], callerIdentity: new FakeIdentity(null))
            .ExecuteChatAsync(new AgentChatRequest("show me issues", ExtraContext: Context("ADMIN", 1, "ISSUE_VIEW")));

        Assert.False(response.Success);
        Assert.Equal("IDENTITY_NOT_VERIFIED", response.Error);
        Assert.False(calledModel);
        Assert.Null(response.ExecutedActions);
    }

    [Fact]
    public async Task NoToken_DoesNothing_EvenWhenTheRequestClaimsARole()
    {
        var calledModel = false;
        var llm = new MockLlmProvider { OnChat = (_, _) => { calledModel = true; return new LlmChatResponse { Content = "hi" }; } };
        var tool = new CapturingTool();

        var response = await new AthennaAgentOrchestrator(llm, [tool], callerIdentity: new FakeIdentity(null, hasCredentials: false))
            .ExecuteChatAsync(new AgentChatRequest("show issues", ExtraContext: Context("ADMIN", 1, "ISSUE_VIEW")));

        Assert.False(response.Success);
        Assert.Equal("IDENTITY_NOT_VERIFIED", response.Error);
        Assert.False(calledModel);
        Assert.Null(tool.Seen);
    }
}

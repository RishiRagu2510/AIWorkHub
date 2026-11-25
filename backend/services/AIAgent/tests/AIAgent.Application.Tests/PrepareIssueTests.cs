using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Domain.Models;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;

public class CreateIssueIntentTests
{
    [Theory]
    [InlineData("create a ticket for role based access", "Role based access")]
    [InlineData("Create an issue to fix the login page.", "Fix the login page")]
    [InlineData("raise a bug: checkout crashes", "Checkout crashes")]
    [InlineData("please create a new task about the nightly job", "The nightly job")]
    [InlineData("add a ticket", "")]
    public void OpensTheForm_WithAShortTitle(string message, string summary)
    {
        Assert.True(CreateIssueIntent.WantsForm(message, out var found));
        Assert.Equal(summary, found);
    }

    [Theory]
    [InlineData("I need to create a new task in FLP360 project", "", "FLP360")]
    [InlineData("create a ticket in the Google04 project for the login page", "The login page", "Google04")]
    [InlineData("we need to raise a bug for PayCard project: checkout crashes", "Checkout crashes", "PayCard")]
    [InlineData("create a ticket for role based access", "Role based access", "")]
    [InlineData("I want to create a new issue", "", "")]
    public void PicksOutTheProjectTheUserNamed_AndKeepsItOutOfTheTitle(string message, string summary, string project)
    {
        Assert.True(CreateIssueIntent.WantsForm(message, out var found, out var named));
        Assert.Equal(summary, found);
        Assert.Equal(project, named);
    }

    [Theory]
    [InlineData("explain issue FLP360-17")]
    [InlineData("create a ticket for FLP360-17")]
    [InlineData("show me the tickets assigned to Rishi")]
    [InlineData("create new employee ticket for Asha")]
    [InlineData("how do I create an issue?")]
    [InlineData("log 2 hours on a task")]
    [InlineData("can i see the issues of Rishi Ragupathi")]
    [InlineData("how many issues are there fo rishi ragupathi")]
    [InlineData("hello")]
    [InlineData("")]
    public void LeavesEverythingElseToTheModel(string message) =>
        Assert.False(CreateIssueIntent.WantsForm(message, out _));
}

public class PrepareIssueToolTests
{
    private static Dictionary<string, object?> Args(string json, long? owner = null)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        if (owner.HasValue) args[IssueVisibility.OwnerArgument] = owner.Value;
        return args;
    }

    private const string Projects =
        "[{\"id\":2,\"key\":\"PAY\",\"projectName\":\"PayCard\"},{\"id\":1,\"key\":\"FLP360\",\"displayName\":\"FLP360 Ongoing Features\"}," +
        "{\"id\":3,\"key\":\"OLD\",\"projectName\":\"Old\",\"status\":\"ARCHIVED\"}]";

    private static JsonDocument Data(ToolResult result)
    {
        Assert.True(result.Success, result.Error);
        return JsonDocument.Parse((string)result.Data!);
    }

    [Fact]
    public async Task OffersTheProjects_AndChoosesNothingForTheUser()
    {
        var (_, factory) = new Routes().Get("/api/projects", Projects).Build();

        var result = await new PrepareIssueTool(factory, new HttpContextAccessor())
            .ExecuteAsync(Args("{\"summary\":\"Add role based access\"}"));

        using var doc = Data(result);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("issueForm").GetBoolean());
        Assert.Equal("Add role based access", root.GetProperty("summary").GetString());
        Assert.Equal(new[] { "FLP360", "PAY" }, root.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("key").GetString()).ToArray());
        Assert.Equal("MEDIUM", root.GetProperty("priority").GetString());
        Assert.False(root.TryGetProperty("assigneeUserId", out _));
        Assert.False(root.TryGetProperty("projectId", out _));
    }

    [Fact]
    public async Task ANamedProject_IsOnlySuggested_NeverChosen()
    {
        var (_, factory) = new Routes().Get("/api/projects", "[{\"id\":1,\"key\":\"FLP360 ONGOING FEATURES AND IMPROVEMENTS\",\"projectName\":\"FLP360 Ongoing\"},{\"id\":4,\"key\":\"GOOGLE04\",\"projectName\":\"Google04\"}]").Build();

        var result = await new PrepareIssueTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{\"project\":\"flp360\"}"));

        using var doc = Data(result);
        Assert.Equal(1, doc.RootElement.GetProperty("suggestedProjectId").GetInt64());
        Assert.False(doc.RootElement.TryGetProperty("projectId", out _));
    }

    [Theory]
    [InlineData("google04", 4)]
    [InlineData("PayCard", 2)]
    [InlineData("pay", 2)]
    public void MatchesTheProjectByKeyOrName(string said, long id)
    {
        var projects = new (long, string?, string?)[] { (1, "FLP360", "FLP360 Ongoing"), (2, "PAY", "PayCard"), (4, "GOOGLE04", "Google04") };
        Assert.Equal(id, PrepareIssueTool.MatchProject(projects, said));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nothing like it")]
    [InlineData("o")]
    public void DoesNotGuessWhenNoneOrSeveralMatch(string said)
    {
        var projects = new (long, string?, string?)[] { (1, "FLP360", "FLP360 Ongoing"), (2, "PAY", "PayCard"), (4, "GOOGLE04", "Google04") };
        Assert.Null(PrepareIssueTool.MatchProject(projects, said));
    }

    [Fact]
    public async Task APersonWhoIsNotAManager_OnlySeesTheirOwnProjects()
    {
        var (handler, factory) = new Routes().Get("/api/projects/by-user/7", Projects).Build();

        var result = await new PrepareIssueTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{}", owner: 7));

        Data(result).Dispose();
        Assert.EndsWith("/api/projects/by-user/7", Assert.Single(handler.Calls).Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OnboardingTemplate_PrefillsTheChecklistAndLabel()
    {
        var (_, factory) = new Routes().Get("/api/projects", Projects).Build();

        var result = await new PrepareIssueTool(factory, new HttpContextAccessor())
            .ExecuteAsync(Args("{\"summary\":\"Onboard Asha\",\"template\":\"EMPLOYEE_ONBOARDING\",\"issueType\":\"BUG\"}"));

        using var doc = Data(result);
        Assert.Contains("Onboarding checklist", doc.RootElement.GetProperty("description").GetString());
        Assert.Equal("onboarding", doc.RootElement.GetProperty("labels")[0].GetString());
        Assert.Equal("TASK", doc.RootElement.GetProperty("issueType").GetString());
    }

    [Fact]
    public async Task NormalisesTypeAndPriority_AndWarnsWhenThereAreNoProjects()
    {
        var (_, factory) = new Routes().Get("/api/projects", "[]").Build();

        var result = await new PrepareIssueTool(factory, new HttpContextAccessor())
            .ExecuteAsync(Args("{\"issueType\":\"nonsense\",\"priority\":\"critical\"}"));

        using var doc = Data(result);
        Assert.Equal("TASK", doc.RootElement.GetProperty("issueType").GetString());
        Assert.Equal("HIGHEST", doc.RootElement.GetProperty("priority").GetString());
        Assert.Contains("not a member", doc.RootElement.GetProperty("note").GetString());
    }
}

public class CreateIssueShortcutTests
{
    private sealed class Named(string name, string permission, string data) : IAgentTool
    {
        public int Calls;
        public Dictionary<string, object?>? Seen;
        public string Name => name;
        public string Description => name;
        public string Category => "GridHub";
        public string RequiredPermission => permission;
        public object ParameterSchema => new { };
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Calls++;
            Seen = arguments;
            return Task.FromResult(new ToolResult(true, data));
        }
    }

    private static Dictionary<string, object> As(params string[] permissions) => new()
    {
        ["role"] = "DEVELOPER",
        ["userId"] = 7L,
        ["permissions"] = permissions
    };

    private const string Form = "{\"issueForm\":true,\"summary\":\"Role based access\",\"projects\":[{\"id\":1,\"key\":\"FLP360\",\"name\":\"FLP360\"}]}";

    [Fact]
    public async Task CreateATicket_OpensTheForm_WithoutAskingTheModel()
    {
        var modelCalls = 0;
        var llm = new MockLlmProvider { OnChat = (_, _) => { modelCalls++; return new LlmChatResponse { Content = "text" }; } };
        var form = new Named("prepare_issue", "ISSUE_CREATE", Form);

        var response = await new AthennaAgentOrchestrator(llm, [form]).ExecuteChatAsync(
            new AgentChatRequest("create a ticket for role based access", ExtraContext: As("ISSUE_CREATE")));

        Assert.Equal(0, modelCalls);
        Assert.Equal("Role based access", form.Seen!["summary"]);
        Assert.False(form.Seen.ContainsKey("project"));
        Assert.Equal("prepare_issue", Assert.Single(response.ExecutedActions!).ToolName);
        Assert.Contains("project first", response.Message);
    }

    [Fact]
    public async Task INeedToCreateATaskInAProject_PassesTheProjectAsASuggestion()
    {
        var llm = new MockLlmProvider { OnChat = (_, _) => new LlmChatResponse { Content = "text" } };
        var form = new Named("prepare_issue", "ISSUE_CREATE", Form);

        await new AthennaAgentOrchestrator(llm, [form]).ExecuteChatAsync(
            new AgentChatRequest("I need to create a new task in FLP360 project", ExtraContext: As("ISSUE_CREATE")));

        Assert.Equal("FLP360", form.Seen!["project"]);
        Assert.False(form.Seen.ContainsKey("summary"));
    }

    [Fact]
    public async Task TheModelIsNeverOfferedCreateIssue_WhenTheFormExists()
    {
        IReadOnlyList<string>? offered = null;
        var llm = new MockLlmProvider
        {
            OnChat = (_, tools) => { offered = tools!.Select(t => t.Name).ToList(); return new LlmChatResponse { Content = "ok" }; }
        };

        await new AthennaAgentOrchestrator(llm, [
            new Named("prepare_issue", "ISSUE_CREATE", Form),
            new Named("create_issue", "ISSUE_CREATE", "{}")
        ]).ExecuteChatAsync(new AgentChatRequest("what can you do", ExtraContext: As("ISSUE_CREATE")));

        Assert.Contains("prepare_issue", offered!);
        Assert.DoesNotContain("create_issue", offered!);
    }

    [Fact]
    public async Task WithoutIssueCreate_NoFormIsOpened()
    {
        var llm = new MockLlmProvider { OnChat = (_, _) => new LlmChatResponse { Content = "You cannot create issues." } };
        var form = new Named("prepare_issue", "ISSUE_CREATE", Form);

        var response = await new AthennaAgentOrchestrator(llm, [form]).ExecuteChatAsync(
            new AgentChatRequest("create a ticket for role based access", ExtraContext: As()));

        Assert.Equal(0, form.Calls);
        Assert.Equal("You cannot create issues.", response.Message);
    }
}

using Xunit;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Domain.Models;

namespace AIAgent.Application.Tests;

public class LogTimeIntentTests
{
    [Theory]
    [InlineData("how can i fill the timesheet")]
    [InlineData("How do I fill my timesheet?")]
    [InlineData("log my time")]
    [InlineData("I want to log work")]
    [InlineData("log time")]
    [InlineData("help me enter my hours")]
    [InlineData("add a work log")]
    [InlineData("fill timesheet")]
    [InlineData("timesheet fill karna hai, how to log")]
    public void OpensTheForm_WhenThePersonWantsToLogTimeButGivesNoDetails(string message) =>
        Assert.True(LogTimeIntent.WantsForm(message));

    [Theory]
    [InlineData("log 2 hours on FLP360-17: fixed the login bug")]
    [InlineData("log 90 minutes on FLP360-42")]
    [InlineData("Log 2.5h on 17")]
    [InlineData("show my timesheet history")]
    [InlineData("am I missing hours this week")]
    [InlineData("approve the timesheet for Asha")]
    [InlineData("what is my timesheet summary")]
    [InlineData("check my logged hours")]
    [InlineData("export timesheet")]
    [InlineData("delete my work log")]
    [InlineData("hello")]
    [InlineData("")]
    public void LeavesEverythingElseToTheModel(string message) =>
        Assert.False(LogTimeIntent.WantsForm(message));

    [Fact]
    public void IgnoresLongMessages() =>
        Assert.False(LogTimeIntent.WantsForm("log my time " + new string('x', 300)));
}

public class LogTimeShortcutTests
{
    private sealed class FormTool(bool succeeds = true) : IAgentTool
    {
        public int Calls;
        public Dictionary<string, object?>? Seen;
        public string Name => "prepare_work_log";
        public string Description => "form";
        public string Category => "Chronos";
        public string RequiredPermission => "WORKLOG_CREATE";
        public object ParameterSchema => new { };
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Calls++;
            Seen = arguments;
            return Task.FromResult(succeeds
                ? new ToolResult(true, "{\"workLogForm\":true,\"today\":\"2026-11-06\",\"issues\":[{\"id\":42,\"issueKey\":\"FLP360-42\",\"summary\":\"x\"}]}")
                : new ToolResult(false, null, "down"));
        }
    }

    private static Dictionary<string, object> As(params string[] permissions) => new()
    {
        ["role"] = "DEVELOPER",
        ["userId"] = 7L,
        ["permissions"] = permissions
    };

    [Fact]
    public async Task HowCanIFillTheTimesheet_OpensTheForm_WithoutAskingTheModel()
    {
        var modelCalls = 0;
        var llm = new MockLlmProvider { OnChat = (_, _) => { modelCalls++; return new LlmChatResponse { Content = "text" }; } };
        var tool = new FormTool();

        var response = await new AthennaAgentOrchestrator(llm, [tool]).ExecuteChatAsync(
            new AgentChatRequest("how can i fill the timesheet", ExtraContext: As("WORKLOG_CREATE")));

        Assert.Equal(0, modelCalls);
        Assert.Equal(1, tool.Calls);
        var action = Assert.Single(response.ExecutedActions!);
        Assert.Equal("prepare_work_log", action.ToolName);
        Assert.True(action.Success);
        Assert.Contains("below", response.Message);
        Assert.Equal(7L, Convert.ToInt64(tool.Seen![IssueVisibility.CallerUserIdArgument]));
        Assert.Equal(7L, Convert.ToInt64(tool.Seen[IssueVisibility.OwnerArgument]));
    }

    [Fact]
    public async Task WithoutTheWorklogPermission_TheFormIsNotOpened_AndTheModelAnswers()
    {
        var llm = new MockLlmProvider { OnChat = (_, _) => new LlmChatResponse { Content = "You cannot log time." } };
        var tool = new FormTool();

        var response = await new AthennaAgentOrchestrator(llm, [tool]).ExecuteChatAsync(
            new AgentChatRequest("how can i fill the timesheet", ExtraContext: As()));

        Assert.Equal(0, tool.Calls);
        Assert.Equal("You cannot log time.", response.Message);
    }

    [Fact]
    public async Task AMessageThatNamesTheTime_GoesToTheModel()
    {
        var llm = new MockLlmProvider { OnChat = (_, _) => new LlmChatResponse { Content = "logged" } };
        var tool = new FormTool();

        var response = await new AthennaAgentOrchestrator(llm, [tool]).ExecuteChatAsync(
            new AgentChatRequest("log 2 hours on FLP360-17", ExtraContext: As("WORKLOG_CREATE")));

        Assert.Equal(0, tool.Calls);
        Assert.Equal("logged", response.Message);
    }

    [Fact]
    public async Task IfTheFormCannotBeBuilt_TheModelStillAnswers()
    {
        var llm = new MockLlmProvider { OnChat = (_, _) => new LlmChatResponse { Content = "Issues are unavailable right now." } };

        var response = await new AthennaAgentOrchestrator(llm, [new FormTool(succeeds: false)]).ExecuteChatAsync(
            new AgentChatRequest("log my time", ExtraContext: As("WORKLOG_CREATE")));

        Assert.Equal("Issues are unavailable right now.", response.Message);
    }
}

using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Domain.Models;
using Xunit;

namespace AIAgent.Application.Tests;

public class LeaveIntentTests
{
    [Theory]
    [InlineData("I want to apply for leave", "")]
    [InlineData("apply leave", "")]
    [InlineData("apply for a leave", "")]
    [InlineData("I need to take leave", "")]
    [InlineData("Can you apply sick leave for me", "SICK")]
    [InlineData("I want to apply wfh", "WFH")]
    [InlineData("request work from home", "WFH")]
    [InlineData("apply floating holiday", "CASUAL")]
    [InlineData("I want to take an unpaid leave", "UNPAID")]
    [InlineData("apply for earned leave", "EARNED")]
    public void OpensTheForm(string message, string kind)
    {
        Assert.True(LeaveIntent.WantsForm(message, out var found));
        Assert.Equal(kind, found);
    }

    [Theory]
    [InlineData("apply sick leave from 10 Nov to 12 Nov")]
    [InlineData("apply leave tomorrow")]
    [InlineData("I want leave on 2026-11-20")]
    [InlineData("show my leave balance")]
    [InlineData("how many leaves do I have")]
    [InlineData("how do I apply for leave")]
    [InlineData("show pending leave requests for my team")]
    [InlineData("cancel my leave")]
    [InlineData("what is the leave policy")]
    [InlineData("who is on leave today")]
    [InlineData("create a ticket for the leave module")]
    [InlineData("")]
    public void LeavesTheseToTheModel(string message)
    {
        Assert.False(LeaveIntent.WantsForm(message));
    }
}

public class LeaveFormShortcutTests
{
    private sealed class Form(string permission) : IAgentTool
    {
        public Dictionary<string, object?>? Seen;
        public string Name => "prepare_leave";
        public string Description => "form";
        public string Category => "Persona";
        public string RequiredPermission => permission;
        public object ParameterSchema => new { };
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Seen = arguments;
            return Task.FromResult(new ToolResult(true, "{\"form\":\"leave\",\"types\":[]}"));
        }
    }

    private static Dictionary<string, object> As(params string[] permissions) => new() { ["role"] = "DEVELOPER", ["userId"] = 7L, ["permissions"] = permissions };

    [Fact]
    public async Task ApplyForLeave_OpensTheForm_WithoutAskingTheModel_AndKeepsAnyTypeTheUserNamed()
    {
        var modelCalls = 0;
        var llm = new MockLlmProvider { OnChat = (_, _) => { modelCalls++; return new LlmChatResponse { Content = "text" }; } };
        var form = new Form("EMPLOYEE_VIEW");

        var response = await new AthennaAgentOrchestrator(llm, [form]).ExecuteChatAsync(
            new AgentChatRequest("I want to apply sick leave", ExtraContext: As("EMPLOYEE_VIEW")));

        Assert.Equal(0, modelCalls);
        Assert.Equal("SICK", form.Seen!["leaveType"]);
        Assert.Equal("prepare_leave", Assert.Single(response.ExecutedActions!).ToolName);
        Assert.Contains("press Apply", response.Message);
    }

    [Fact]
    public async Task WithoutThePermission_NoFormIsOpened()
    {
        var llm = new MockLlmProvider { OnChat = (_, _) => new LlmChatResponse { Content = "text" } };
        var form = new Form("EMPLOYEE_VIEW");

        var response = await new AthennaAgentOrchestrator(llm, [form]).ExecuteChatAsync(
            new AgentChatRequest("apply leave", ExtraContext: As("ISSUE_VIEW")));

        Assert.Null(form.Seen);
        Assert.DoesNotContain(response.ExecutedActions ?? [], a => a.ToolName == "prepare_leave");
    }
}

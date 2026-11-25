using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Domain.Models;
using Xunit;

namespace AIAgent.Application.Tests;

public class MockLlmProvider : ILlmProvider
{
    public Func<List<AgentMessage>, IEnumerable<IAgentTool>?, LlmChatResponse>? OnChat { get; set; }
    public Func<string, string, string>? OnGenerateText { get; set; }

    public Task<LlmChatResponse> ChatAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? availableTools = null, string? model = null, CancellationToken cancellationToken = default)
    {
        if (OnChat != null)
        {
            return Task.FromResult(OnChat(messages, availableTools));
        }

        return Task.FromResult(new LlmChatResponse { Content = "Default Athenna response" });
    }

    public Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (OnGenerateText != null)
        {
            return Task.FromResult(OnGenerateText(systemPrompt, userPrompt));
        }

        return Task.FromResult($"Generated: {userPrompt}");
    }
}

public class MockTool : IAgentTool
{
    public string Name => "test_tool";
    public string Description => "A test tool";
    public string Category => "Test";
    public string RequiredPermission => "TEST_PERM";
    public object ParameterSchema => new { };

    public bool WasExecuted { get; private set; }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        WasExecuted = true;
        return Task.FromResult(new ToolResult(true, new { result = "success" }));
    }
}

public class AthennaAgentOrchestratorTests
{
    [Fact]
    public async Task Chat_WithoutToolCalls_ReturnsDirectResponse()
    {
        var mockLlm = new MockLlmProvider
        {
            OnChat = (msgs, tools) => new LlmChatResponse { Content = "Hello, I am Athenna!" }
        };

        var orchestrator = new AthennaAgentOrchestrator(mockLlm, []);
        var request = new AgentChatRequest("Hi Athenna");

        var response = await orchestrator.ExecuteChatAsync(request);

        Assert.True(response.Success);
        Assert.Equal("Hello, I am Athenna!", response.Message);
        Assert.Empty(response.ExecutedActions!);
    }

    [Fact]
    public async Task Chat_WithAuthorizedToolCalls_ExecutesToolAndFeedsResult()
    {
        var tool = new MockTool();
        int step = 0;

        var mockLlm = new MockLlmProvider
        {
            OnChat = (msgs, tools) =>
            {
                step++;
                if (step == 1)
                {
                    
                    return new LlmChatResponse
                    {
                        ToolCalls = [new ToolCall { Name = "test_tool", ArgumentsJson = "{}" }]
                    };
                }

                
                return new LlmChatResponse { Content = "Tool execution finished successfully." };
            }
        };

        var orchestrator = new AthennaAgentOrchestrator(mockLlm, [tool]);
        var request = new AgentChatRequest(
            Message: "Execute the test tool",
            ExtraContext: new Dictionary<string, object>
            {
                ["role"] = "ADMIN",
                ["permissions"] = new[] { "TEST_PERM" }
            }
        );

        var response = await orchestrator.ExecuteChatAsync(request);

        Assert.True(response.Success);
        Assert.True(tool.WasExecuted);
        Assert.Single(response.ExecutedActions!);
        Assert.Equal("test_tool", response.ExecutedActions![0].ToolName);
        Assert.Equal("Tool execution finished successfully.", response.Message);
    }

    [Fact]
    public async Task Chat_WithoutPermission_BlocksToolWithRbacDenied()
    {
        var tool = new MockTool();
        int step = 0;

        var mockLlm = new MockLlmProvider
        {
            OnChat = (msgs, tools) =>
            {
                step++;
                if (step == 1)
                {
                    
                    return new LlmChatResponse
                    {
                        ToolCalls = [new ToolCall { Name = "test_tool", ArgumentsJson = "{}" }]
                    };
                }

                
                return new LlmChatResponse { Content = "I cannot execute this because you lack the required permission." };
            }
        };

        var orchestrator = new AthennaAgentOrchestrator(mockLlm, [tool]);
        var request = new AgentChatRequest(
            Message: "Execute unauthorized action",
            ExtraContext: new Dictionary<string, object>
            {
                ["role"] = "DEVELOPER",
                ["permissions"] = new[] { "ISSUE_VIEW" } 
            }
        );

        var response = await orchestrator.ExecuteChatAsync(request);

        Assert.True(response.Success);
        Assert.False(tool.WasExecuted); 
        Assert.Single(response.ExecutedActions!);
        Assert.False(response.ExecutedActions![0].Success);
        Assert.Contains("RBAC Access Denied", response.ExecutedActions![0].Result?.ToString() ?? "");
    }

    [Fact]
    public async Task Copilot_GeneratesStructuredOutput()
    {
        var mockLlm = new MockLlmProvider
        {
            OnGenerateText = (sys, user) => $"BDD Criteria for: {user}"
        };

        var orchestrator = new AthennaAgentOrchestrator(mockLlm, []);
        var request = new SingleCallCopilotRequest("GenerateIssueAcceptanceCriteria", "Create SSO login button");

        var response = await orchestrator.ExecuteCopilotAsync(request);

        Assert.True(response.Success);
        Assert.Contains("BDD Criteria for: Create SSO login button", response.OutputText);
    }
}

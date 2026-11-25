using System.Text;
using AIAgent.Api.Controllers;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Infrastructure.Llm;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIAgent.Application.Tests;

public class AgentControllerAuthTests
{
    private sealed class FakeOrchestrator : IAthennaAgentOrchestrator
    {
        public int Chats { get; private set; }
        public int Copilots { get; private set; }

        public Task<AgentChatResponse> ExecuteChatAsync(AgentChatRequest request, CancellationToken cancellationToken = default, IAgentProgress? progress = null)
        {
            Chats++;
            return Task.FromResult(new AgentChatResponse("s", "hello", null, true));
        }

        public Task<SingleCallCopilotResponse> ExecuteCopilotAsync(SingleCallCopilotRequest request, CancellationToken cancellationToken = default)
        {
            Copilots++;
            return Task.FromResult(new SingleCallCopilotResponse("text", null, true));
        }
    }

    private sealed class Identity(VerifiedCaller? caller, bool credentials) : ICallerIdentityProvider
    {
        public bool HasCredentials => credentials;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default) => Task.FromResult(caller);
    }

    private static readonly VerifiedCaller Asha = new(7, "DEVELOPER", ["DEVELOPER"], [], 1);

    private static (AthennaAgentController Controller, FakeOrchestrator Orchestrator, MemoryStream Body) Make(VerifiedCaller? caller, bool credentials)
    {
        var orchestrator = new FakeOrchestrator();
        var config = new ConfigurationBuilder().Build();
        var controller = new AthennaAgentController(
            orchestrator,
            NullLogger<AthennaAgentController>.Instance,
            config,
            new OpenAiChatClient(new HttpClient(), config, NullLogger<OpenAiChatClient>.Instance),
            new GroqChatClient(new HttpClient(), config, NullLogger<GroqChatClient>.Instance),
            new Identity(caller, credentials),
            new NudgeService([], new Identity(caller, credentials)));

        var body = new MemoryStream();
        var http = new DefaultHttpContext();
        http.Response.Body = body;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, orchestrator, body);
    }

    [Theory]
    [InlineData(false)]   
    [InlineData(true)]    
    public async Task Chat_RefusesWhoeverIsNotSignedIn(bool hasToken)
    {
        var (controller, orchestrator, _) = Make(caller: null, credentials: hasToken);

        var result = await controller.Chat(new AgentChatRequest("hi", ExtraContext: new() { ["role"] = "ADMIN" }), default);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(0, orchestrator.Chats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nudges_AreRefusedForWhoeverIsNotSignedIn(bool hasToken)
    {
        var (controller, _, _) = Make(caller: null, credentials: hasToken);

        Assert.IsType<UnauthorizedObjectResult>(await controller.GetNudges(default));
    }

    [Fact]
    public async Task Nudges_AreGivenToASignedInPerson()
    {
        var (controller, _, _) = Make(Asha, credentials: true);

        var result = Assert.IsType<OkObjectResult>(await controller.GetNudges(default));
        Assert.Empty(Assert.IsType<NudgeReport>(result.Value).Items);   
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Models_AreNotShownToWhoeverIsNotSignedIn(bool hasToken)
    {
        var (controller, _, _) = Make(caller: null, credentials: hasToken);

        Assert.IsType<UnauthorizedObjectResult>(await controller.GetModels(default));
    }

    [Fact]
    public async Task Models_AreShownToASignedInPerson()
    {
        var (controller, _, _) = Make(Asha, credentials: true);

        var result = Assert.IsType<OkObjectResult>(await controller.GetModels(default));
        Assert.Contains("providers", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public void Status_StaysOpen_ForHealthChecks()
    {
        var (controller, _, _) = Make(caller: null, credentials: false);

        Assert.IsType<OkObjectResult>(controller.GetStatus());
    }

    [Fact]
    public async Task Chat_AnswersASignedInPerson()
    {
        var (controller, orchestrator, _) = Make(Asha, credentials: true);

        var result = await controller.Chat(new AgentChatRequest("hi"), default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, orchestrator.Chats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatStream_RefusesWhoeverIsNotSignedIn_WithoutOpeningAStream(bool hasToken)
    {
        var (controller, orchestrator, body) = Make(caller: null, credentials: hasToken);

        await controller.ChatStream(new AgentChatRequest("hi"), default);

        Assert.Equal(StatusCodes.Status401Unauthorized, controller.Response.StatusCode);
        Assert.DoesNotContain("text/event-stream", controller.Response.ContentType ?? "");
        Assert.Contains("Sign in again", Encoding.UTF8.GetString(body.ToArray()));
        Assert.Equal(0, orchestrator.Chats);
    }

    [Fact]
    public async Task ChatStream_StreamsForASignedInPerson()
    {
        var (controller, orchestrator, body) = Make(Asha, credentials: true);

        await controller.ChatStream(new AgentChatRequest("hi"), default);

        Assert.Equal(1, orchestrator.Chats);
        Assert.Contains("event: done", Encoding.UTF8.GetString(body.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Copilot_RefusesWhoeverIsNotSignedIn(bool hasToken)
    {
        var (controller, orchestrator, _) = Make(caller: null, credentials: hasToken);

        var result = await controller.Copilot(new SingleCallCopilotRequest("DraftWorklog", "fixed login"), default);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(0, orchestrator.Copilots);
    }

    [Fact]
    public async Task Copilot_ServesASignedInPerson()
    {
        var (controller, orchestrator, _) = Make(Asha, credentials: true);

        var result = await controller.Copilot(new SingleCallCopilotRequest("DraftWorklog", "fixed login"), default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, orchestrator.Copilots);
    }
}

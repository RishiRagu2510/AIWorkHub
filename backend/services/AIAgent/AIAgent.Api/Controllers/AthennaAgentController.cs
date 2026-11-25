using AIAgent.Contracts.Dtos;
using AIAgent.Api.Streaming;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Infrastructure.Llm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIAgent.Api.Controllers;

[ApiController]
[Route("api/v1/agent")]
public class AthennaAgentController : ControllerBase
{
    private readonly IAthennaAgentOrchestrator _orchestrator;
    private readonly ILogger<AthennaAgentController> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAiChatClient _openAi;
    private readonly GroqChatClient _groq;
    private readonly ICallerIdentityProvider _identity;
    private readonly NudgeService _nudges;

    public AthennaAgentController(
        IAthennaAgentOrchestrator orchestrator,
        ILogger<AthennaAgentController> logger,
        IConfiguration configuration,
        OpenAiChatClient openAi,
        GroqChatClient groq,
        ICallerIdentityProvider identity,
        NudgeService nudges)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _configuration = configuration;
        _openAi = openAi;
        _groq = groq;
        _identity = identity;
        _nudges = nudges;
    }

    private const string SignInMessage = "Sign in again to use Athenna.";

    private async Task<bool> SignedInAsync(CancellationToken cancellationToken) =>
        _identity.HasCredentials && await _identity.VerifyAsync(cancellationToken) is { UserId: > 0 };

    [HttpGet("models")]
    public async Task<IActionResult> GetModels(CancellationToken cancellationToken)
    {
        if (!await SignedInAsync(cancellationToken)) return Unauthorized(new { error = SignInMessage });
        return Ok(new { providers = ProviderAvailability.Describe(_configuration, _openAi, _groq).Select(p => new { id = p.Id, name = p.Name, available = p.Available, reason = p.Reason }) });
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AgentChatResponse>> Chat([FromBody] AgentChatRequest request, CancellationToken cancellationToken)
    {
        if (!await SignedInAsync(cancellationToken)) return Unauthorized(new { error = SignInMessage });

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message cannot be empty." });
        }

        var response = await _orchestrator.ExecuteChatAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] AgentChatRequest request, CancellationToken cancellationToken)
    {
        if (!await SignedInAsync(cancellationToken))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsJsonAsync(new { error = SignInMessage }, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "Message cannot be empty." }, cancellationToken);
            return;
        }

        await using var stream = new SseAgentProgress(Response, cancellationToken);
        await stream.StartAsync();

        try
        {
            var response = await _orchestrator.ExecuteChatAsync(request, cancellationToken, stream);
            await stream.SendAsync("done", response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streamed chat failed.");
            try { await stream.SendAsync("error", new { message = "Athenna could not finish this request. Please try again." }); }
            catch (Exception) {  }
        }
    }

    [HttpPost("copilot")]
    public async Task<ActionResult<SingleCallCopilotResponse>> Copilot([FromBody] SingleCallCopilotRequest request, CancellationToken cancellationToken)
    {
        if (!await SignedInAsync(cancellationToken)) return Unauthorized(new { error = SignInMessage });

        if (string.IsNullOrWhiteSpace(request.Feature) || string.IsNullOrWhiteSpace(request.InputText))
        {
            return BadRequest(new { error = "Feature and InputText are required." });
        }

        var response = await _orchestrator.ExecuteCopilotAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("nudges")]
    public async Task<IActionResult> GetNudges(CancellationToken cancellationToken)
    {
        var report = await _nudges.GetAsync(cancellationToken);
        return report is null ? Unauthorized(new { error = SignInMessage }) : Ok(report);
    }

    [AIAgent.Api.Licensing.AllowWithoutAthennaLicense]
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            name = "Athenna",
            service = "AIAgent Engine",
            status = "Operational",
            version = "1.0.0",
            supportedPortals = new[] { "AIWorkHub", "GridHub", "Persona", "Chronos" }
        });
    }
}

using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIAgent.Api.Controllers;

[ApiController]
[Route("api/v1/agent/actions")]
public class AiActionsController(IAiActionLog log, ICallerIdentityProvider identity, ILogger<AiActionsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? userId, [FromQuery] string? outcome, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!identity.HasCredentials) return Unauthorized(new { error = "Sign in again to see the assistant's activity." });
        var caller = await identity.VerifyAsync(ct);
        if (caller is not { UserId: > 0, TenantId: > 0 }) return Unauthorized(new { error = "Sign in again to see the assistant's activity." });

        var seesEveryone = caller.Roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase))
                           || caller.Permissions.Contains("AUDIT_VIEW", StringComparer.OrdinalIgnoreCase);

        
        var scope = seesEveryone ? userId : caller.UserId;

        try
        {
            var items = await log.ListAsync(caller.TenantId, scope, outcome, take, ct);
            return Ok(new
            {
                scope = seesEveryone ? "workspace" : "own",
                items = items.Select(a => new
                {
                    a.UserId,
                    a.UserName,
                    a.Role,
                    a.SessionId,
                    tool = a.ToolName,
                    kind = a.Kind.ToString(),
                    a.Outcome,
                    a.Arguments,
                    a.Detail,
                    flagged = a.InjectionFlagged,
                    at = a.OccurredAt
                })
            });
        }
        catch (Exception ex) when (ex is MongoDB.Driver.MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "The AI action log is unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The assistant's activity log is not reachable right now." });
        }
    }
}

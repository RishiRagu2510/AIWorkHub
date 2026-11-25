using IdentityTenant.Application.Plans;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/tenants/current/plan")]
public sealed class TenantPlanController(
    TenantPlanService plans,
    ITenantContext actor,
    ILogger<TenantPlanController> logger) : ControllerBase
{
    private bool IsAdmin => actor.Roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase));

    private object Present(AccessRequest r)
    {
        return new
        {
            r.Id, r.Kind, r.Name, r.Action, r.UserId, r.UserName, r.Note, r.CreatedAt, mine = r.UserId == actor.UserId
        };
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!actor.IsAuthenticated) return Unauthorized();
        var plan = await plans.GetPlanAsync(actor.TenantId, actor.UserId, IsAdmin, ct);
        return Ok(new
        {
            portals = plan.Portals.Select(p => new { id = p.Id, included = p.Included }),
            features = plan.Features.Select(f => new { id = f.Id, included = f.Included, requires = f.Requires }),
            isAdmin = plan.IsAdmin,
            requests = plan.Requests.Select(Present)
        });
    }


    [HttpDelete("requests/{requestId}")]
    public async Task<IActionResult> Cancel(string requestId, CancellationToken ct)
    {
        if (!actor.IsAuthenticated) return Unauthorized();
        var cancelled = await plans.CancelRequestAsync(actor.TenantId, actor.UserId, IsAdmin, requestId, ct);
        if (cancelled is null) return NotFound(new { error = "Request not found." });

        logger.LogInformation("Tenant {TenantId}: user {UserId} cancelled the request for {Kind} {Name}.",
            actor.TenantId, actor.UserId, cancelled.Kind, cancelled.Name);
        return NoContent();
    }

    [HttpPost("requests")]
    public async Task<IActionResult> Request([FromBody] AccessRequestInput? input, CancellationToken ct)
    {
        if (!actor.IsAuthenticated) return Unauthorized();
        var result = await plans.RequestAsync(actor.TenantId, actor.UserId, IsAdmin,
            input is null ? null : new PlanChangeInput(input.Portal, input.Feature, input.Note, input.Action), ct);

        switch (result.Status)
        {
            case RequestStatus.Existing:
                return Ok(Present(result.Request!));
            case RequestStatus.Created:
                logger.LogInformation("Tenant {TenantId}: user {UserId} asked to {Action} {Kind} {Name}.",
                    actor.TenantId, actor.UserId, result.Request!.Action, result.Request.Kind, result.Request.Name);
                return Created($"api/tenants/current/plan/requests/{result.Request.Id}", Present(result.Request));
            default:
                object body = result.Requires is not null
                    ? new { error = result.Error, code = result.Code, requires = result.Requires }
                    : result.Code is not null
                        ? new { error = result.Error, code = result.Code }
                        : new { error = result.Error };
                return result.Status switch
                {
                    RequestStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, body),
                    RequestStatus.Conflict => Conflict(body),
                    _ => BadRequest(body)
                };
        }
    }

    public sealed record AccessRequestInput(string? Portal, string? Feature, string? Note, string? Action = null);
}
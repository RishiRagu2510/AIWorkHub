using System.Security.Cryptography;
using System.Text;
using IdentityTenant.Application.Plans;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/platform/tenants/{tenantId:long}/plan")]
public sealed class PlatformTenantPlanController(
    TenantPlanService plans,
    IConfiguration configuration,
    ILogger<PlatformTenantPlanController> logger) : ControllerBase
{
    public const string KeyHeader = "X-Operator-Key";

    private IActionResult? Deny()
    {
        var configured = configuration["Platform:OperatorKey"];
        if (string.IsNullOrWhiteSpace(configured)) return NotFound();
        var sent = Request.Headers[KeyHeader].ToString();

        var same = !string.IsNullOrEmpty(sent) && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(sent)), SHA256.HashData(Encoding.UTF8.GetBytes(configured)));
        return same ? null : Unauthorized(new { error = "A valid operator key is required." });
    }

    private static PlanResponse Respond(long tenantId, PlanSelection plan)
    {
        return new PlanResponse(tenantId, plan.Portals?.ToArray(), plan.Features?.ToArray());
    }

    [HttpGet]
    public async Task<IActionResult> Get(long tenantId, CancellationToken ct)
    {
        if (Deny() is { } denied) return denied;
        var plan = await plans.DescribePlanAsync(tenantId, ct);
        return plan is null ? NotFound(new { error = "Tenant not found." }) : Ok(Respond(tenantId, plan));
    }


    [HttpGet("requests")]
    public async Task<IActionResult> Requests(long tenantId, CancellationToken ct)
    {
        if (Deny() is { } denied) return denied;
        var open = await plans.OpenRequestsAsync(tenantId, ct);
        return open is null ? NotFound(new { error = "Tenant not found." }) : Ok(open);
    }


    [HttpDelete("requests/{requestId}")]
    public async Task<IActionResult> Dismiss(long tenantId, string requestId, CancellationToken ct)
    {
        if (Deny() is { } denied) return denied;
        var result = await plans.DeclineAsync(tenantId, requestId, ct);
        return result.Outcome == DecisionOutcome.Done ? NoContent() : NotFound(new { error = "Request not found." });
    }


    [HttpPost("requests/{requestId}/approve")]
    public async Task<IActionResult> Approve(long tenantId, string requestId, CancellationToken ct)
    {
        if (Deny() is { } denied) return denied;
        var result = await plans.ApproveAsync(tenantId, requestId, ct);
        return result.Outcome switch
        {
            DecisionOutcome.NotFound => NotFound(new { error = "Request not found." }),
            DecisionOutcome.LastPortal => Conflict(new
                { error = "A plan has to include at least one portal.", code = "LAST_PORTAL" }),
            DecisionOutcome.NeedsPortal => Conflict(new
            {
                error = "This comes with a portal the company does not have yet. Approve that first.",
                code = "REQUIRES_PORTAL", requires = result.Needs
            }),
            _ => NoContent()
        };
    }

    [HttpPut]
    public async Task<IActionResult> Put(long tenantId, [FromBody] PlanRequest? request, CancellationToken ct)
    {
        if (Deny() is { } denied) return denied;
        if (request is null) return BadRequest(new { error = "A plan is required." });
        var result = await plans.SetPlanAsync(tenantId, request.Portals, request.Features, ct);
        switch (result.Status)
        {
            case SetPlanStatus.Invalid: return BadRequest(new { error = result.Error });
            case SetPlanStatus.TenantNotFound: return NotFound(new { error = "Tenant not found." });
        }

        var plan = Respond(tenantId, result.Plan!);
        logger.LogInformation("Plan for tenant {TenantId} set: portals {Portals}, features {Features}.", tenantId,
            plan.Portals is null ? "unrestricted" : string.Join(",", plan.Portals),
            plan.Features is null ? "unrestricted" : string.Join(",", plan.Features));
        return Ok(plan);
    }


    public sealed record PlanRequest(string[]? Portals, string[]? Features);

    public sealed record PlanResponse(long TenantId, string[]? Portals, string[]? Features);
}
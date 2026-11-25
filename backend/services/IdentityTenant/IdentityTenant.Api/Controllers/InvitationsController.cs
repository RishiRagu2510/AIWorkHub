using Authorization;
using IdentityTenant.Api.Services.Invitations;
using IdentityTenant.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/invitations")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN")]
[RequirePermission("USER_VIEW")]
public sealed class InvitationsController(InvitationService invitations) : ControllerBase
{
    private (long Tenant, long User)? Actor()
    {
        var tenants = User.FindAll(TenantClaimTypes.TenantId).ToArray();
        var users = User.FindAll(TenantClaimTypes.UserId).ToArray();
        return tenants.Length == 1 && users.Length == 1 && long.TryParse(tenants[0].Value, out var tenant) &&
               tenant > 0 && long.TryParse(users[0].Value, out var user) && user > 0
            ? (tenant, user)
            : null;
    }

    [HttpGet]
    public async Task<IActionResult> List(int page = 1, CancellationToken ct = default)
    {
        if (Actor() is not { } actor) return Unauthorized();
        var result = await invitations.List(actor.Tenant, actor.User, page, ct);
        return result.Outcome switch
        {
            InvitationOutcome.Ok => Ok(result.Page), InvitationOutcome.NotAdmin => Forbid(), _ => BadRequest()
        };
    }

    [HttpPost]
    public async Task<IActionResult> Create(InviteRequest request, CancellationToken ct)
    {
        if (Actor() is not { } actor) return Unauthorized();
        var result = await invitations.Create(actor.Tenant, actor.User, request, ct);
        return result.Outcome switch
        {
            InvitationOutcome.Ok => Created("/api/invitations", result.Invitation),
            InvitationOutcome.NotAdmin => Forbid(),
            InvitationOutcome.Invalid => BadRequest(new { code = result.Code }),
            _ => Conflict(new { code = result.Code })
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        if (Actor() is not { } actor) return Unauthorized();
        var result = await invitations.Revoke(actor.Tenant, actor.User, id, ct);
        return result.Outcome switch
        {
            InvitationOutcome.Ok => NoContent(),
            InvitationOutcome.NotAdmin => Forbid(),
            InvitationOutcome.NotFound => NotFound(),
            _ => Conflict(new { code = result.Code })
        };
    }
}
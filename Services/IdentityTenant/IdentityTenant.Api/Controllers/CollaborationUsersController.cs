using Authorization;
using IdentityTenant.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/users/{id:long}/collaboration-eligibility")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequirePermission("ISSUE_COMMENT_CREATE")]
public sealed class CollaborationUsersController(IUserDirectory directory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var claims = User.FindAll(TenantClaimTypes.TenantId).ToArray();
        if (claims.Length != 1 || !long.TryParse(claims[0].Value, out var tenant) || tenant <= 0) return Unauthorized();
        var user = await directory.GetCollaborationUserAsync(tenant, id, ct);
        return user is null ? NotFound() : Ok(user);
    }
}
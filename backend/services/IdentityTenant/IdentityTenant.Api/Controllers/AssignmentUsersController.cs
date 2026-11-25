using Authorization;
using IdentityTenant.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/users/{userId:long}/assignment-eligibility")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequirePermission("ISSUE_ASSIGN")]
public sealed class AssignmentUsersController(IUserDirectory directory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(long userId, CancellationToken ct)
    {
        var claims = User.FindAll(TenantClaimTypes.TenantId).ToArray();
        if (claims.Length != 1 || !long.TryParse(claims[0].Value, out var tenantId) || tenantId <= 0)
            return Unauthorized();
        var user = await directory.GetAssignmentUserAsync(tenantId, userId, ct);
        return user is null ? NotFound() : Ok(user);
    }
}
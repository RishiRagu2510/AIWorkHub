using Authorization;
using IdentityTenant.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN")]
[RequirePermission("USER_VIEW")]
public sealed class UsersController(IUserDirectory directory) : ControllerBase
{
    private long TenantId => User.FindAll(TenantClaimTypes.TenantId).Count() == 1 &&
                             long.TryParse(User.FindFirst(TenantClaimTypes.TenantId)?.Value, out var id) && id > 0
        ? id
        : 0;

    [HttpGet]
    public async Task<IActionResult> List(string role = "DEVELOPER", int page = 1, int pageSize = 100,
        CancellationToken ct = default)
    {
        if (TenantId == 0) return Unauthorized();
        if (!string.Equals(role, "DEVELOPER", StringComparison.OrdinalIgnoreCase) || page < 1 || page > 100000 ||
            pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Use role=DEVELOPER, page >= 1 and pageSize between 1 and 100." });
        return Ok(await directory.ListDevelopersAsync(TenantId, page, pageSize, ct));
    }

    [HttpGet("{userId:long}")]
    public async Task<IActionResult> Get(long userId, CancellationToken ct)
    {
        if (TenantId == 0) return Unauthorized();
        var user = await directory.GetDeveloperAsync(TenantId, userId, ct);
        return user is null ? NotFound() : Ok(user);
    }
}
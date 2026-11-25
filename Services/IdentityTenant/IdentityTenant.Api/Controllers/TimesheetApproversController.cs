using Authorization;
using IdentityTenant.Api.Services.Timesheets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/users/timesheet-approvers")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequirePermission("TIMESHEET_VIEW")]
public sealed class TimesheetApproversController(TimesheetApproverService approvers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, CancellationToken ct = default)
    {
        var claims = User.FindAll(TenantClaimTypes.TenantId).ToArray();
        if (claims.Length != 1 || !long.TryParse(claims[0].Value, out var tenant) || tenant <= 0) return Unauthorized();
        if (page < 1 || page > 10000) return BadRequest();
        return Ok(await approvers.List(tenant, page, ct));
    }
}
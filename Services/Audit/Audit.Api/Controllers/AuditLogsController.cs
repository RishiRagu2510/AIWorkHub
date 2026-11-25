using Audit.Application.Services;
using Audit.Contracts;
using Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Audit.Api.Controllers;

[ApiController]
[Route("api/audit/logs")]
[Authorize]
public sealed class AuditLogsController : ControllerBase
{
    private readonly AuditLogService _auditLogService;

    public AuditLogsController(AuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    [RequirePermission("AUDIT_VIEW")]
    public async Task<ActionResult<AuditLogPageDto>> Search(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken)
    {
        var tenantClaim =
            User.FindFirst(Authentication.AiWorkHubClaimTypes.TenantId)?.Value;

        if (!long.TryParse(tenantClaim, out var tenantId) ||
            tenantId <= 0 ||
            User.FindAll(Authentication.AiWorkHubClaimTypes.TenantId).Count() != 1)
        {
            return Unauthorized(new
            {
                message =
                    "Tenant information is missing from the authenticated user."
            });
        }

        var result =
            await _auditLogService.SearchAsync(
                tenantId,
                query,
                cancellationToken);

        return Ok(result);
    }
}

using Authorization;
using Jira.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Jira.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/assignment-members")]
[Authorize]
[RequirePermission("ISSUE_ASSIGN")]
public sealed class AssignmentMembersController(ProjectMembershipService service, ITenantContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(long projectId, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        var members = await service.ListEffectiveAsync(context.TenantId, projectId, ct);
        
        return Ok(members.Select(m => new { m.UserId }));
    }
}

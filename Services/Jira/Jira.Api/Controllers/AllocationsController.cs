using Authorization;
using Jira.Contracts.Projects;
using Jira.Infrastructure.Persistence.Allocations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Jira.Api.Controllers;


[ApiController, Route("api/projects/allocations"), Authorize]
public sealed class AllocationsController(AllocationService allocations, ITenantContext actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(long? userId, CancellationToken ct)
    {
        if (!actor.IsAuthenticated) return Unauthorized();
        var target = userId ?? actor.UserId;
        if (target != actor.UserId && !(User.IsInRole("ADMIN") && User.HasClaim(TenantClaimTypes.Permission, "PROJECT_MANAGE"))) return Forbid();
        return Ok(await allocations.List(target, ct));
    }

    [HttpPost, Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE"), RequirePermission("ISSUE_ASSIGN")]
    public async Task<IActionResult> Create(AllocationRequest request, CancellationToken ct)
    {
        if (!actor.IsAuthenticated) return Unauthorized();
        var result = await allocations.Create(request, ct);
        return result.Outcome switch
        {
            AllocationOutcome.Created => StatusCode(201),
            AllocationOutcome.InvalidDates => BadRequest(new { message = result.Message }),
            AllocationOutcome.ProjectNotFound => NotFound(),
            AllocationOutcome.ProjectNotActive => BadRequest(),
            _ => Conflict(new { message = result.Message })
        };
    }

    [HttpDelete("{id:long}"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (!actor.IsAuthenticated) return Unauthorized();
        return await allocations.Delete(id, ct) ? NoContent() : NotFound();
    }
}

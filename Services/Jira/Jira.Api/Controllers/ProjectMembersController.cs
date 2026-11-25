using Authorization;
using Jira.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Jira.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/members")]
[Authorize(Roles = "ADMIN")]
[RequirePermission("PROJECT_MANAGE")]
[RequirePermission("USER_VIEW")]
public sealed class ProjectMembersController(ProjectMembershipService service, ITenantContext context) : ControllerBase
{
    public sealed record AddMemberRequest(long UserId);
    [HttpGet]
    public async Task<IActionResult> List(long projectId, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        return Ok(await service.ListAsync(context.TenantId, projectId, ct));
    }
    [HttpGet("effective")]
    public async Task<IActionResult> Effective(long projectId, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        return Ok(await service.ListEffectiveAsync(context.TenantId, projectId, ct));
    }
    [HttpPost]
    public async Task<IActionResult> Add(long projectId, AddMemberRequest request, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        try { await service.AddAsync(context.TenantId, projectId, request.UserId, ct); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "User directory is unavailable. Please try again." }); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(503, new { message = "User directory is unavailable. Please try again." }); }
        return NoContent();
    }
    [HttpDelete("{userId:long}")]
    public async Task<IActionResult> Remove(long projectId, long userId, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        await service.RemoveAsync(context.TenantId, projectId, userId, ct);
        return NoContent();
    }
}

using Authorization;
using Jira.Infrastructure.Persistence.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;
namespace Jira.Api.Controllers;
[ApiController]
[Route("api/notifications")]
[Authorize]
[RequirePermission("ISSUE_VIEW")]
public sealed class NotificationsController(NotificationInbox inbox, ITenantContext context) : ControllerBase
{
    private bool CanReview => User.HasClaim(TenantClaimTypes.Permission,"TIMESHEET_TEAM_VIEW");
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20, bool unreadOnly = false, CancellationToken ct = default)
        => !context.IsAuthenticated ? Unauthorized() : Ok(await inbox.ListAsync(page, pageSize, unreadOnly, ct, CanReview));
    [HttpGet("unread-count")]
    public async Task<IActionResult> Count(CancellationToken ct)
        => !context.IsAuthenticated ? Unauthorized() : Ok(new { unreadCount = await inbox.CountAsync(ct, CanReview) });
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        await inbox.ReadAsync(id, ct, CanReview); return NoContent();
    }
    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        return Ok(new { updatedCount = await inbox.ReadAllAsync(ct, CanReview) });
    }
}

using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIAgent.Api.Controllers;

[ApiController]
[Route("api/v1/agent/memory")]
public class MemoryController(IUserMemoryStore store, ICallerIdentityProvider identity, ILogger<MemoryController> logger) : ControllerBase
{
    private async Task<IActionResult> GuardedAsync(Func<long, long, Task<IActionResult>> action, CancellationToken ct)
    {
        var caller = identity.HasCredentials ? await identity.VerifyAsync(ct) : null;
        if (caller is not { UserId: > 0, TenantId: > 0 }) return Unauthorized(new { error = "Sign in again to see what Athenna remembers." });
        try
        {
            return await action(caller.TenantId, caller.UserId);
        }
        catch (UserMemoryUnavailableException ex)
        {
            logger.LogWarning(ex, "Memory is unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        GuardedAsync(async (tenant, user) =>
        {
            var notes = await store.ListAsync(tenant, user, ct);
            return Ok(new { notes = notes.Select(n => new { id = n.Id, text = n.Text, createdAt = n.CreatedAt }), limit = UserMemoryRules.MaxItems });
        }, ct);

    [HttpDelete("{id}")]
    public Task<IActionResult> Delete(string id, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) => await store.DeleteAsync(tenant, user, id, ct) ? NoContent() : NotFound(), ct);

    [HttpDelete]
    public Task<IActionResult> Clear(CancellationToken ct) =>
        GuardedAsync(async (tenant, user) => Ok(new { removed = await store.ClearAsync(tenant, user, ct) }), ct);
}

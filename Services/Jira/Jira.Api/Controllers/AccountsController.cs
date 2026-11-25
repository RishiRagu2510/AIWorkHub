using Authorization;
using Jira.Contracts.Projects;
using Jira.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Jira.Api.Controllers;


[ApiController]
[Route("api/projects/accounts")]
public sealed class AccountsController(AccountService accounts, ITenantContext context) : ControllerBase
{
    [HttpGet, RequirePermission("PROJECT_VIEW")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        context.IsAuthenticated ? Ok(await accounts.List(ct)) : Unauthorized();

    [HttpPost, Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> Create(AccountRequest request, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        var account = await accounts.Create(request, ct);
        Response.Headers.ETag = $"\"{account.Version}\"";
        return StatusCode(201, account);
    }

    [HttpPut("{id:long}"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> Update(long id, AccountRequest request, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        var account = await accounts.Update(id, request, ct);
        Response.Headers.ETag = $"\"{account.Version}\"";
        return Ok(account);
    }

    [HttpDelete("{id:long}"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_DELETE")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (!context.IsAuthenticated) return Unauthorized();
        await accounts.Delete(id, ct);
        return NoContent();
    }
}

using Authorization;
using Persona.Api.Services.Employees;
using Persona.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Persona.Api.Controllers;

[RequirePortal("employee")]
[ApiController]
[Route("api/employees/setup")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN,HR,HUMAN_RESOURCE")]
[EmployeeErrors]
public sealed class EmployeeSetupController(EmployeeSetupService setup) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        return Ok(await setup.CatalogAsync(ct));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(string? search, long? roleId = null, int page = 1,
        CancellationToken ct = default)
    {
        return Ok(await setup.UsersAsync(search, roleId, page, ct));
    }


    [HttpPost("users/{userId:long}/roles")]
    [HttpPut("users/{userId:long}/roles")]
    public async Task<IActionResult> AssignRoles(long userId, AssignRolesInput input, CancellationToken ct)
    {
        return Ok(await setup.AssignRolesAsync(userId, input, HttpMethods.IsPut(Request.Method), ct));
    }

    [HttpPost("users/bulk-roles")]
    public async Task<IActionResult> BulkAssignRoles(BulkAssignRolesInput input, CancellationToken ct)
    {
        return Ok(await setup.BulkAssignRolesAsync(input, ct));
    }

    [HttpDelete("{kind}")]
    public async Task<IActionResult> DeleteAll(string kind, CancellationToken ct)
    {
        return Ok(await setup.DeleteAllAsync(kind, ct));
    }

    [HttpDelete("{kind}/{id:long}")]
    public async Task<IActionResult> Delete(string kind, long id, CancellationToken ct)
    {
        await setup.DeleteAsync(kind, id, ct);
        return NoContent();
    }

    [HttpPost("{kind}")]
    public async Task<IActionResult> Create(string kind, SetupInput input, CancellationToken ct)
    {
        return Ok(await setup.SaveAsync(kind, null, input, ct));
    }

    [HttpPut("{kind}/{id:long}")]
    public async Task<IActionResult> Update(string kind, long id, SetupInput input, CancellationToken ct)
    {
        return Ok(await setup.SaveAsync(kind, id, input, ct));
    }
}
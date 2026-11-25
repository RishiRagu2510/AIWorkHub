using Authentication;
using Authorization;
using Persona.Api.Services.Employees;
using Persona.Contracts;
using Persona.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Persona.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EmployeeErrors]
[Route("api/employees")]
public sealed class EmployeesController(
    EmployeeService employees,
    EmployeeAccess access,
    ITenantContext actor,
    PersonaDbContext db) : ControllerBase
{
    [HttpGet("manager-options")]
    [RequirePermission("EMPLOYEE_VIEW")]
    [RequirePortal("employee")]
    public async Task<IActionResult> ManagerOptions(long? employeeId, CancellationToken ct)
    {
        if (!access.Can("EMPLOYEE_CREATE") && !access.Can("EMPLOYEE_UPDATE")) return Forbid();
        return Ok(await employees.ManagerOptionsAsync(employeeId, ct));
    }

    [HttpGet("login-users")]
    [RequirePermission("EMPLOYEE_CREATE")]
    [RequirePortal("employee")]
    public async Task<IActionResult> LoginUsers(string? search = null, int page = 1, CancellationToken ct = default)
    {
        return Ok(await employees.LoginUsersAsync(search, page, ct));
    }

    [HttpGet]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> List([FromQuery] EmployeeSearch q, CancellationToken ct)
    {
        var page = await employees.ListAsync(q, ct);
        return page is null ? Forbid() : Ok(page);
    }


    [HttpGet("export-details")]
    [Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE")]
    [RequirePermission("EMPLOYEE_VIEW_SENSITIVE")]
    [RequirePortal("employee")]
    public async Task<IActionResult> ExportDetails(string? status = null, int limit = EmployeeExportReader.MaxEmployees,
        CancellationToken ct = default)
    {
        if (limit < 1 || limit > EmployeeExportReader.MaxEmployees) throw new EmployeeProblem(400, "Invalid limit.");
        return Ok(
            await EmployeeExportReader.ReadAsync(db, actor.TenantId, status?.Trim().ToUpperInvariant(), limit, ct));
    }

    [HttpGet("org-chart")]
    [RequirePermission("EMPLOYEE_VIEW")]
    [RequirePortal("employee")]
    public async Task<IActionResult> Chart(long? managerId, int page = 1, string? roleLevel = null,
        CancellationToken ct = default)
    {
        return Ok(await employees.OrgChartAsync(managerId, page, roleLevel, ct));
    }

    [HttpGet("me")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        return Profile(await employees.MeAsync(ct));
    }

    [HttpGet("{id:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        return Profile(await employees.GetAsync(id, ct));
    }

    [HttpGet("{id:long}/career-history")]
    [RequirePermission("EMPLOYEE_VIEW")]
    [RequirePortal("employee")]
    public async Task<IActionResult> CareerHistory(long id, CancellationToken ct)
    {
        return Ok(await employees.CareerHistoryAsync(id, ct));
    }

    [HttpPost]
    [RequirePermission("EMPLOYEE_CREATE")]
    [RequirePortal("employee")]
    public async Task<IActionResult> Create(EmployeeRequest r, CancellationToken ct)
    {
        return CreatedProfile(await employees.CreateAsync(r, ct));
    }

    [HttpPost("from-onboarding/{issueId:long}")]
    [Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE")]
    [RequirePermission("EMPLOYEE_CREATE")]
    [RequirePortal("employee")]
    public async Task<IActionResult> CreateFromOnboarding(long issueId, EmployeeRequest r,
        [FromServices] OnboardingApprovalSigner approvals, CancellationToken ct)
    {
        var approval = approvals.Verify(Request.Headers[OnboardingApprovalSigner.Header].ToString(), actor.TenantId,
            issueId, DateTimeOffset.UtcNow);
        if (approval is null)
            return StatusCode(403,
                new
                {
                    message = "A current approval from the onboarding workflow is required.",
                    code = "ONBOARDING_APPROVAL_REQUIRED"
                });
        return CreatedProfile(await employees.CreateFromOnboardingAsync(r, issueId, approval, ct));
    }

    [HttpPut("{id:long}")]
    [RequirePermission("EMPLOYEE_UPDATE")]
    [RequirePortal("employee")]
    public async Task<IActionResult> Update(long id, EmployeeRequest r, CancellationToken ct)
    {
        return Profile(await employees.UpdateAsync(id, r, ct));
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("EMPLOYEE_DELETE")]
    [RequirePortal("employee")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await employees.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("me/profile")]
    [RequirePermission("EMPLOYEE_UPDATE_SELF")]
    [RequirePortal("employee")]
    public async Task<IActionResult> Self(EmployeeSelfRequest r, CancellationToken ct)
    {
        return Profile(await employees.UpdateSelfAsync(r, ct));
    }

    [HttpGet("career-catalog")]
    [RequirePermission("EMPLOYEE_VIEW")]
    [RequirePortal("employee")]
    public async Task<IActionResult> CareerCatalog(CancellationToken ct)
    {
        return Ok(await employees.CareerCatalogAsync(ct));
    }

    [HttpGet("references")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> References(CancellationToken ct)
    {
        return Ok(await employees.ReferencesAsync(ct));
    }

    [HttpPost("references")]
    [RequirePermission("EMPLOYEE_MANAGE_ORG")]
    [RequirePortal("employee")]
    public async Task<IActionResult> AddReference(EmployeeReferenceRequest r, CancellationToken ct)
    {
        return Created("/api/employees/references", await employees.AddReferenceAsync(r, ct));
    }

    [HttpPut("references/{id:long}")]
    [RequirePermission("EMPLOYEE_MANAGE_ORG")]
    [RequirePortal("employee")]
    public async Task<IActionResult> UpdateReference(long id, EmployeeReferenceRequest r, CancellationToken ct)
    {
        return Ok(await employees.UpdateReferenceAsync(id, r, Request.Headers.IfMatch.ToString(), ct));
    }


    private IActionResult Profile(EmployeeProfileDto profile)
    {
        Response.Headers.ETag = $"\"{profile.Version}\"";
        return Ok(profile);
    }

    private IActionResult CreatedProfile(EmployeeProfileDto profile)
    {
        Response.Headers.ETag = $"\"{profile.Version}\"";
        return Created($"/api/employees/{profile.Id}", profile);
    }
}
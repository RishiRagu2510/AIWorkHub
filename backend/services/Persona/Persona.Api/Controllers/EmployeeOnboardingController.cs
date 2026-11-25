using Authorization;
using Persona.Api.Services.Employees;
using Persona.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Persona.Api.Controllers;

[RequirePortal("employee")]
[ApiController]
[Route("api/employees/onboarding")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "TALENT_ACQUISITION")]
[EmployeeErrors]
public sealed class EmployeeOnboardingController(EmployeeOnboardingService onboarding) : ControllerBase
{
    [HttpPost("validate")]
    [RequirePermission("ISSUE_CREATE", "EMPLOYEE_CREATE")]
    public async Task<IActionResult> Validate(EmployeeOnboardingRequest proposal, CancellationToken ct)
    {
        return Ok(await onboarding.Validate(proposal, ct));
    }

    [HttpGet("options")]
    [RequirePermission("ISSUE_CREATE", "EMPLOYEE_CREATE")]
    public async Task<IActionResult> Options(CancellationToken ct)
    {
        return Ok(await onboarding.Options(ct));
    }
}
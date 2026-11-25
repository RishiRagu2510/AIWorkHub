using Authorization;
using Persona.Api.Services.Employees;
using Persona.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Persona.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "Bearer")]
[EmployeeErrors]
[RequirePermission("EMPLOYEE_VIEW")]
[Route("api/employees/absence")]
public sealed class EmployeeAbsenceController(AbsenceService absence) : ControllerBase
{
    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy(int year, CancellationToken ct)
    {
        return Ok(await absence.GetPolicy(year, ct));
    }

    [HttpPut("policy")]
    [RequirePermission("EMPLOYEE_UPDATE")]
    public async Task<IActionResult> SavePolicy(int year, AbsencePolicyInput input, CancellationToken ct)
    {
        return Ok(await absence.SavePolicy(year, input, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(int year = 0, bool team = false, bool all = false, int page = 1,
        CancellationToken ct = default)
    {
        return Ok(await absence.List(year, team, all, page, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(AbsenceInput input, CancellationToken ct)
    {
        return Ok(await absence.Create(input, ct));
    }

    [HttpPost("{id:long}/decision")]
    public async Task<IActionResult> Decide(long id, AbsenceDecision input, CancellationToken ct)
    {
        return Ok(await absence.Decide(id, input, User.IsInRole("HR") || User.IsInRole("HUMAN_RESOURCE"), ct));
    }
}
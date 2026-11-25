using Authorization;
using Jira.Api.Onboarding;
using Jira.Api.Services.Onboarding;
using Jira.Contracts.Issues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;
using IdentityTenant.Contracts;

namespace Jira.Api.Controllers;


[ApiController, Authorize, Route("api/issues")]
public sealed class EmployeeRequestsController(EmployeeRequestService requests) : ControllerBase
{
    [HttpPost("employee-requests"), Authorize(Roles = "TALENT_ACQUISITION"), RequirePermission("ISSUE_CREATE"), RequirePortal("employee")]
    public async Task<IActionResult> Create(CreateEmployeeRequest input, CancellationToken ct)
    {
        var assigns = input.Issue.AssigneeUserId.HasValue || (input.Issue.AssigneeUserIds != null && input.Issue.AssigneeUserIds.Length > 0);
        if (assigns && !User.HasClaim(TenantClaimTypes.Permission, "ISSUE_ASSIGN")) return Forbid();
        var result = await requests.Create(input, ct);
        return result.Rejection is { } rejection ? StatusCode(rejection.Status, rejection.Body) : Created($"/api/issues/{result.Issue!.Id}", result.Issue);
    }

    [HttpGet("{id:long}/employee-request"), RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => View(await requests.Get(id, ct));

    [HttpPost("{id:long}/employee-request/review"), Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE"), RequirePermission("EMPLOYEE_CREATE"), RequirePermission("ISSUE_VIEW"), RequirePortal("employee")]
    public async Task<IActionResult> Review(long id, ReviewEmployeeRequest input, CancellationToken ct)
    {
        if (input.Decision is not ("APPROVED" or "REJECTED")) return BadRequest(new { message = "Choose approve or reject." });
        return View(await requests.Review(id, input, ct));
    }

    [HttpPost("{id:long}/employee-request/complete"), Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE"), RequirePermission("EMPLOYEE_CREATE"), RequirePermission("ISSUE_VIEW"), RequirePortal("employee")]
    public async Task<IActionResult> Complete(long id, EmployeeRequest input, CancellationToken ct)
    {
        var result = await requests.Complete(id, input, ct);
        return result.Rejection is { } rejection ? StatusCode(rejection.Status, rejection.Body) : View(result.Request);
    }

    private IActionResult View(IEmployeeRequestView? view) => view is null ? NotFound() : Ok(view);
}

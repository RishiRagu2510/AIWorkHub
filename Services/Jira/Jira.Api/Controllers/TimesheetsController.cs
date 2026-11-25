using Authorization;
using Jira.Contracts.Timesheets;
using Jira.Infrastructure.Persistence.Timesheets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;
namespace Jira.Api.Controllers;
[RequirePortal("timesheet")]
[ApiController,Route("api/timesheets"),Authorize,RequirePermission("TIMESHEET_VIEW")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
[ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
public sealed class TimesheetsController(TimesheetService service) : ControllerBase
{
    [HttpGet("hierarchy")]
    [EndpointDescription("Paginated user/project time groups with authorized Epic → Story → Task/Bug → Subtask ancestors. Own time by default; TIMESHEET_TEAM_VIEW enables team=true. Node minutes are direct time, never double-count ancestor totals.")]
    [ProducesResponseType<TimesheetHierarchyPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TimesheetHierarchyPage>> Hierarchy([FromQuery] TimesheetQuery query, bool team = false, CancellationToken ct = default)
    {
        if (team && !User.HasClaim(TenantClaimTypes.Permission, "TIMESHEET_TEAM_VIEW"))
            return Forbid();
        return await service.Hierarchy(query, team, ct);
    }
    [HttpGet("me")]
[EndpointDescription("Own work logs and DB aggregates for an inclusive daily, weekly, monthly or custom range. Requires TIMESHEET_VIEW.")]
    [ProducesResponseType<TimesheetReport>(StatusCodes.Status200OK)]
    public Task<TimesheetReport> Me([FromQuery]TimesheetQuery query,CancellationToken ct)=>service.Report(query,false,ct);
    [HttpGet("team"),RequirePermission("TIMESHEET_TEAM_VIEW")]
[EndpointDescription("Team logs and aggregates within the authenticated tenant and existing Jira issue scope. Requires TIMESHEET_VIEW and TIMESHEET_TEAM_VIEW.")]
    [ProducesResponseType<TimesheetReport>(StatusCodes.Status200OK)]
    public Task<TimesheetReport> Team([FromQuery]TimesheetQuery query,CancellationToken ct)=>service.Report(query,true,ct);
    [HttpGet("summary")]
[EndpointDescription("Alias of me. Requires TIMESHEET_VIEW.")]
    [ProducesResponseType<TimesheetReport>(StatusCodes.Status200OK)]
    public Task<TimesheetReport> Summary([FromQuery]TimesheetQuery query,CancellationToken ct)=>service.Report(query,false,ct);
    [HttpGet("report"),RequirePermission("TIMESHEET_TEAM_VIEW")]
[EndpointDescription("Alias of team. Requires TIMESHEET_VIEW and TIMESHEET_TEAM_VIEW.")]
    [ProducesResponseType<TimesheetReport>(StatusCodes.Status200OK)]
    public Task<TimesheetReport> Report([FromQuery]TimesheetQuery query,CancellationToken ct)=>service.Report(query,true,ct);
    [HttpGet("{periodId:long}/approvers")]
[EndpointDescription("Eligible active tenant reviewer candidates, excluding self. Submission revalidates access to all period issues.")]
    [ProducesResponseType<IReadOnlyList<TimesheetApprover>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<TimesheetApprover>> Approvers(long periodId,CancellationToken ct)=>service.Approvers(periodId,ct);
    [HttpPost("submit")]
[EndpointDescription("Submit own full Monday–Sunday period to an eligible approver. Requires TIMESHEET_VIEW and quoted If-Match version. Optional Idempotency-Key.")]
    [ProducesResponseType<PeriodDto>(StatusCodes.Status200OK)]
    public Task<PeriodDto> Submit(PeriodAction request,CancellationToken ct)=>service.Act("submit",request,ct);
    [HttpPost("approve"),RequirePermission("TIMESHEET_APPROVE"),RequirePermission("TIMESHEET_TEAM_VIEW")]
[EndpointDescription("Approve a submitted period within issue scope. Requires TIMESHEET_VIEW, TIMESHEET_TEAM_VIEW and TIMESHEET_APPROVE. No self review. Quoted If-Match required; optional Idempotency-Key.")]
    [ProducesResponseType<PeriodDto>(StatusCodes.Status200OK)]
    public Task<PeriodDto> Approve(PeriodAction request,CancellationToken ct)=>service.Act("approve",request,ct);
    [HttpPost("reject"),RequirePermission("TIMESHEET_APPROVE"),RequirePermission("TIMESHEET_TEAM_VIEW")]
[EndpointDescription("Reject a submitted period with a reason (1–2000 characters). Same permissions, scope and headers as approve.")]
    [ProducesResponseType<PeriodDto>(StatusCodes.Status200OK)]
    public Task<PeriodDto> Reject(PeriodAction request,CancellationToken ct)=>service.Act("reject",request,ct);
    public sealed record ReviewRequest(long? ApproverUserId=null,string? Reason=null);
    [HttpPost("{id:long}/submit")]
    public Task<PeriodDto> SubmitById(long id,ReviewRequest request,CancellationToken ct)=>service.Act("submit",new(id,request.ApproverUserId),ct);
    [HttpPost("{id:long}/approve"),RequirePermission("TIMESHEET_APPROVE"),RequirePermission("TIMESHEET_TEAM_VIEW")]
    public Task<PeriodDto> ApproveById(long id,CancellationToken ct)=>service.Act("approve",new(id),ct);
    [HttpPost("{id:long}/reject"),RequirePermission("TIMESHEET_APPROVE"),RequirePermission("TIMESHEET_TEAM_VIEW")]
    public Task<PeriodDto> RejectById(long id,ReviewRequest request,CancellationToken ct)=>service.Act("reject",new(id,Reason:request.Reason),ct);
}

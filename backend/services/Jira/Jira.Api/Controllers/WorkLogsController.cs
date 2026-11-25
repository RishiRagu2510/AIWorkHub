using Authorization;
using Jira.Contracts.Timesheets;
using Jira.Infrastructure.Persistence.Timesheets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;
namespace Jira.Api.Controllers;
[ApiController,Route("api/worklogs"),Authorize]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
[ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
public sealed class WorkLogsController(TimesheetService service) : ControllerBase
{
    private bool Team=>User.HasClaim(TenantClaimTypes.Permission,"TIMESHEET_TEAM_VIEW");
    [HttpGet,RequirePermission("WORKLOG_VIEW")]
    [EndpointSummary("List authorized work logs")]
    [EndpointDescription("Requires WORKLOG_VIEW. Own logs by default; TIMESHEET_TEAM_VIEW allows logs within the existing Jira issue scope. Dates are inclusive (maximum 366 days), pageSize 1–100.")]
    [ProducesResponseType<WorkLogPage>(StatusCodes.Status200OK)]
    public Task<WorkLogPage> List([FromQuery]TimesheetQuery query,CancellationToken ct)=>service.List(query,Team,ct);
    [HttpGet("{id:long}"),RequirePermission("WORKLOG_VIEW")]
    [EndpointSummary("Read an authorized work log with its current version")]
    [ProducesResponseType<WorkLogDto>(StatusCodes.Status200OK)]
    public Task<WorkLogDto> Get(long id,CancellationToken ct)=>service.Get(id,Team,ct);
    [HttpPost,RequirePermission("WORKLOG_CREATE")]
    [EndpointSummary("Log work on an accessible issue")]
    [EndpointDescription("Requires WORKLOG_CREATE. Tenant and user come from authentication; project comes from the issue. Integer minutes 1–1440, daily total at most 1440; description up to 4000 characters. Optional Idempotency-Key supports safe retries.")]
    [ProducesResponseType<WorkLogDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkLogDto>> Create(WorkLogRequest request,CancellationToken ct) {
        var item=await service.Create(request,ct);return CreatedAtAction(nameof(Get),new{id=item.Id},item);
    }
    [HttpPut("{id:long}"),RequirePermission("WORKLOG_UPDATE")]
    [EndpointSummary("Update an owned draft or rejected work log")]
    [EndpointDescription("Requires WORKLOG_UPDATE and quoted If-Match containing version. Issue cannot change. Submitted/approved periods are locked. Optional Idempotency-Key supports safe retries.")]
    [ProducesResponseType<WorkLogDto>(StatusCodes.Status200OK)]
    public Task<WorkLogDto> Update(long id,WorkLogRequest request,CancellationToken ct)=>service.Update(id,request,ct);
    [HttpDelete("{id:long}"),RequirePermission("WORKLOG_DELETE")]
    [EndpointSummary("Delete an owned draft or rejected work log")]
    [EndpointDescription("Requires WORKLOG_DELETE and quoted If-Match containing version. Submitted/approved periods are locked.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id,CancellationToken ct){await service.Delete(id,ct);return NoContent();}
}

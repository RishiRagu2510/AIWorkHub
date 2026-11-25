using Authorization;
using Jira.Api.Collaboration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;
namespace Jira.Api.Controllers;
[ApiController]
[Authorize]
public sealed class CollaborationController(CollaborationService service,WorkflowService workflow,Jira.Api.Services.Planning.PlanningService planning,ITenantContext context):ControllerBase
{
    [HttpGet("api/issues/{id:long}/activity"),RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Activity(long id,int page=1,CancellationToken ct=default)=>Ok(await service.History(id,page,ct));
    [HttpGet("api/issues/{id:long}/watchers"),RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Watchers(long id,CancellationToken ct)=>Ok(await service.Watchers(id,ct));
    [HttpPut("api/issues/{id:long}/watchers/me"),RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Watch(long id,CancellationToken ct){await service.Watch(id,true,ct);return NoContent();}
    [HttpDelete("api/issues/{id:long}/watchers/me"),RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Unwatch(long id,CancellationToken ct){await service.Watch(id,false,ct);return NoContent();}
    [HttpGet("api/projects/{id:long}/workflow"),RequirePermission("PROJECT_VIEW")]
    public async Task<IActionResult> Workflow(long id,CancellationToken ct)=>Ok(await workflow.Get(context.TenantId,id,ct));
    public sealed record ConfigureWorkflow(WorkflowDefinition Definition,uint ExpectedVersion);
    [HttpPut("api/projects/{id:long}/workflow"),Authorize(Roles="ADMIN"),RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> Configure(long id,ConfigureWorkflow request,CancellationToken ct){await workflow.Put(context.TenantId,id,request.Definition,request.ExpectedVersion,ct);return Ok(await workflow.Get(context.TenantId,id,ct));}
    [HttpGet("api/boards"),RequirePermission("PROJECT_VIEW")]
    public async Task<IActionResult> Boards(long projectId,CancellationToken ct)=>Ok(await planning.Boards(projectId,ct));
    [HttpGet("api/boards/{id:long}"),RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Board(long id,int page=1,CancellationToken ct=default)=>Ok(await service.BoardView(id,page,ct));
    public sealed record CreateBoard(long ProjectId,string Name,string BoardType="KANBAN");
    [HttpPost("api/boards"),Authorize(Roles="ADMIN"),RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> Create(CreateBoard request,CancellationToken ct){var id=await service.CreateBoard(request.ProjectId,request.Name,request.BoardType,await workflow.Definition(context.TenantId,request.ProjectId,ct),ct);return Created($"/api/boards/{id}",new{id});}
    [HttpGet("api/activity"),Authorize(Roles="ADMIN"),RequirePermission("AUDIT_VIEW")]
    public async Task<IActionResult> Feed(int page=1,CancellationToken ct=default){var feed=await service.Feed(page,ct);return feed is null?BadRequest():Ok(feed);}
}

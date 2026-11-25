using Authorization;
using Jira.Api.Services.Planning;
using Jira.Contracts.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jira.Api.Controllers;


[ApiController, Authorize]
public sealed class PlanningController(PlanningService planning) : ControllerBase
{
    [HttpGet("api/projects/{projectId:long}/components"), RequirePermission("PROJECT_VIEW")]
    public async Task<IActionResult> Components(long projectId, CancellationToken ct) => Ok(await planning.Components(projectId, ct));

    [HttpPost("api/projects/{projectId:long}/components"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> CreateComponent(long projectId, NamedRequest request, CancellationToken ct) =>
        Created($"/api/projects/{projectId}/components", await planning.CreateComponent(projectId, request, ct));

    [HttpPut("api/projects/{projectId:long}/components/{id:long}"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> UpdateComponent(long projectId, long id, NamedRequest request, CancellationToken ct) =>
        Ok(await planning.UpdateComponent(projectId, id, request, ct));

    [HttpGet("api/projects/{projectId:long}/versions"), RequirePermission("PROJECT_VIEW")]
    public async Task<IActionResult> Versions(long projectId, CancellationToken ct) => Ok(await planning.Versions(projectId, ct));

    [HttpPost("api/projects/{projectId:long}/versions"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> CreateVersion(long projectId, VersionRequest request, CancellationToken ct) =>
        Created($"/api/projects/{projectId}/versions", await planning.CreateVersion(projectId, request, ct));

    [HttpPut("api/projects/{projectId:long}/versions/{id:long}"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> UpdateVersion(long projectId, long id, VersionRequest request, CancellationToken ct) =>
        Ok(await planning.UpdateVersion(projectId, id, request, ct));

    [HttpGet("api/projects/{projectId:long}/labels"), RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Labels(long projectId, CancellationToken ct) => Ok(await planning.Labels(projectId, ct));

    [HttpGet("api/boards/{boardId:long}/sprints"), RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Sprints(long boardId, CancellationToken ct) => Ok(await planning.Sprints(boardId, ct));

    [HttpPost("api/boards/{boardId:long}/sprints"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> CreateSprint(long boardId, SprintRequest request, CancellationToken ct) =>
        Created($"/api/boards/{boardId}/sprints", await planning.CreateSprint(boardId, request, ct));

    [HttpPost("api/sprints/{id:long}/start"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> Start(long id, CancellationToken ct) => Ok(await planning.StartSprint(id, ct));

    [HttpPost("api/sprints/{id:long}/complete"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE")]
    public async Task<IActionResult> Complete(long id, CancellationToken ct) => Ok(await planning.CompleteSprint(id, ct));

    [HttpGet("api/boards/{boardId:long}/backlog"), RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Backlog(long boardId, long? sprintId = null, int page = 1, CancellationToken ct = default) =>
        Ok(await planning.Backlog(boardId, sprintId, page, ct));

    [HttpPut("api/boards/{boardId:long}/issues/{issueId:long}/placement"), Authorize(Roles = "ADMIN"), RequirePermission("PROJECT_MANAGE"), RequirePermission("ISSUE_UPDATE")]
    public async Task<IActionResult> Place(long boardId, long issueId, Placement request, CancellationToken ct)
    {
        await planning.Place(boardId, issueId, request, ct);
        return NoContent();
    }
}

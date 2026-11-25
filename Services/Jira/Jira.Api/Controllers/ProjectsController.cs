using Microsoft.EntityFrameworkCore;
using Authorization;
using Jira.Contracts.Projects;
using Microsoft.AspNetCore.Authorization;
using Jira.Application.DTOs;
using Jira.Application.Services;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Jira.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;
    private readonly ITenantContext _tenantContext;
    

    public ProjectsController(
        ProjectService projectService,
        ITenantContext tenantContext)
    {
        _projectService = projectService;
        _tenantContext = tenantContext;
    }

    [Authorize(Roles = "ADMIN")]
    [RequirePermission("PROJECT_UPDATE", "PROJECT_MANAGE")]
    [HttpPut("{projectId:long}")]
    public async Task<IActionResult> Update(long projectId, UpdateProjectRequest request, CancellationToken ct)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();
        return Ok(await _projectService.UpdateAsync(_tenantContext.TenantId, projectId, request, ct));
    }

    [Authorize(Roles = "ADMIN")]
    [RequirePermission("PROJECT_UPDATE", "PROJECT_MANAGE")]
    [HttpPut("{projectId:long}/parent")]
    public async Task<IActionResult> Move(long projectId, MoveProjectRequest request, CancellationToken ct)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();
        return Ok(await _projectService.MoveAsync(_tenantContext.TenantId, projectId, request.ParentProjectId, ct));
    }

    [Authorize(Roles = "ADMIN")]
    [RequirePermission("PROJECT_DELETE")]
    [HttpDelete("{projectId:long}")]
    public async Task<IActionResult> Delete(long projectId, CancellationToken ct)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();
        await _projectService.DeleteAsync(_tenantContext.TenantId, projectId, ct);
        return NoContent();
    }

    [Authorize(Roles = "ADMIN")]
    [RequirePermission("PROJECT_CREATE", "PROJECT_MANAGE")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken ct)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();
        var project = await _projectService.CreateAsync(_tenantContext.TenantId, request, ct);
        return CreatedAtAction(nameof(GetById), new { projectId = project.Id }, project);
    }

    [Authorize(Roles = "ADMIN")]
    [RequirePermission("PROJECT_CREATE", "PROJECT_MANAGE")]
    [HttpPost("{projectId:long}/subprojects")]
    public Task<IActionResult> CreateSubproject(long projectId, CreateProjectRequest request, CancellationToken ct)
    {
        if (request.ParentProjectId.HasValue && request.ParentProjectId != projectId)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Parent project does not match the route." }));
        return Create(request with { ParentProjectId = projectId }, ct);
    }

    [RequirePermission("PROJECT_VIEW")]
    [HttpGet("{projectId:long}/subprojects")]
    public async Task<IActionResult> GetSubprojects(long projectId, CancellationToken ct)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();
        return Ok(await _projectService.GetChildrenAsync(_tenantContext.TenantId, projectId, ct));
    }

    [RequirePermission("PROJECT_VIEW")]
    [HttpGet("by-user/{userId:long}")]
    public async Task<IActionResult> ByUser(long userId,
        [FromServices] Jira.Infrastructure.Persistence.JiraDbContext db,
        [FromServices] Jira.Infrastructure.Persistence.JiraResourceScope scope,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var memberships = db.Projects.FromSqlInterpolated($"""
            WITH RECURSIVE employee_projects AS (
                SELECT p.*, p.xmin FROM projects p WHERE p.tenant_id={tenantId}
                AND (p.project_lead_user_id={userId} OR EXISTS (
                    SELECT 1 FROM project_members m WHERE m.tenant_id={tenantId} AND m.project_id=p.id AND m.user_id={userId}))
                UNION
                SELECT p.*, p.xmin FROM projects p JOIN employee_projects parent
                ON p.parent_project_id=parent.id AND p.tenant_id=parent.tenant_id
            ) SELECT * FROM employee_projects
            """);
        var visible = scope.Projects(tenantId);
        var issues = scope.Issues(tenantId);
        var canViewIssues = User.HasClaim(TenantContext.TenantClaimTypes.Permission, "ISSUE_VIEW");
        return Ok(await memberships.Where(p => p.TenantId == tenantId).OrderBy(p => p.Id)
            .Select(p => new {
                p.Id,
                projectName = p.DisplayName != "" ? p.DisplayName : p.Name,
                p.Key,
                p.Status,
                role = p.ProjectLeadUserId == userId ? "LEAD" : db.ProjectMembers.Where(m => m.TenantId == tenantId && m.ProjectId == p.Id && m.UserId == userId).Select(m => m.Role).FirstOrDefault() ?? "INHERITED",
                issueCount = canViewIssues && visible.Any(v => v.Id == p.Id) ? (int?)issues.Count(i => i.ProjectId == p.Id) : null
            }).ToListAsync(ct));
    }

    [RequirePermission("PROJECT_VIEW")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var projects = await _projectService.GetAllAsync(
            _tenantContext.TenantId,
            cancellationToken);

        return Ok(projects);
    }

    [RequirePermission("PROJECT_VIEW")]
    [HttpGet("{projectId:long}")]
    public async Task<ActionResult<ProjectDto>> GetById(
        long projectId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var project = await _projectService.GetByIdAsync(
            _tenantContext.TenantId,
            projectId,
            cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        return Ok(project);
    }
    
    [HttpGet("context")]
    [RequirePermission("PROJECT_VIEW")]
    public IActionResult Context()
    {
        return Ok(new
        {
            isAuthenticated = User.Identity?.IsAuthenticated ?? false,
            userId = _tenantContext.UserId,
            tenantId = _tenantContext.TenantId,
            roles = _tenantContext.Roles
        });
    }
}
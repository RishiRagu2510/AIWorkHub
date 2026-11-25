using Authorization;
using Microsoft.AspNetCore.Authorization;
using Jira.Application.Abstractions.Services;
using Jira.Contracts.Issues;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Jira.Api.Controllers;

[ApiController]
[Route("api/issues")]
public sealed class IssuesController : ControllerBase
{
    private readonly IIssueService _issueService;
    private readonly ITenantContext _tenantContext;

    public IssuesController(
        IIssueService issueService,
        ITenantContext tenantContext)
    {
        _issueService = issueService;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    [RequirePermission("ISSUE_CREATE")]
    public async Task<IActionResult> Create(
        [FromBody] CreateIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        if ((request.AssigneeUserId.HasValue || (request.AssigneeUserIds != null && request.AssigneeUserIds.Length > 0)) &&
            !(await HttpContext.RequestServices.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(User, null, "Permission:ISSUE_ASSIGN")).Succeeded)
            return Forbid();

        var issue = await _issueService.CreateAsync(
            _tenantContext.TenantId,
            _tenantContext.UserId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                issueId = issue.Id
            },
            issue);
    }

    [HttpGet("{issueId:long}")]
    [RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> GetById(
        long issueId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var issue = await _issueService.GetByIdAsync(
            _tenantContext.TenantId,
            issueId,
            cancellationToken);

        if (issue is null)
        {
            return NotFound();
        }

        return Ok(issue);
    }

    [HttpGet("key/{issueKey}")]
    [RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> GetByKey(
        string issueKey,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var issue = await _issueService.GetByKeyAsync(
            _tenantContext.TenantId,
            issueKey,
            cancellationToken);

        if (issue is null)
        {
            return NotFound();
        }

        return Ok(issue);
    }

    [HttpGet("project/{projectId:long}")]
    [RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> GetByProject(
        long projectId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var issues = await _issueService.GetByProjectAsync(
            _tenantContext.TenantId,
            projectId,
            cancellationToken);

        return Ok(issues);
    }

    [HttpPut("{issueId:long}")]
    [RequirePermission("ISSUE_UPDATE")]
    public async Task<IActionResult> Update(
        long issueId,
        [FromBody] UpdateIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        if ((request.AssigneeUserId.HasValue || (request.AssigneeUserIds != null && request.AssigneeUserIds.Length > 0) || request.ClearAssignee) &&
            !(await authorization.AuthorizeAsync(User, null, "Permission:ISSUE_ASSIGN")).Succeeded)
            return Forbid();
        if (request.StatusCode is not null &&
            !(await authorization.AuthorizeAsync(User, null, "Permission:ISSUE_TRANSITION")).Succeeded)
            return Forbid();

        var issue = await _issueService.UpdateAsync(
            _tenantContext.TenantId,
            _tenantContext.UserId,
            issueId,
            request,
            cancellationToken);

        if (issue is null)
        {
            return NotFound();
        }

        return Ok(issue);
    }

    [HttpPut("{issueId:long}/assignee")]
    [RequirePermission("ISSUE_ASSIGN")]
    public async Task<IActionResult> Assign(
        long issueId,
        [FromBody] AssignIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var issue = await _issueService.AssignAsync(
            _tenantContext.TenantId,
            _tenantContext.UserId,
            issueId,
            request,
            cancellationToken);

        if (issue is null)
        {
            return NotFound();
        }

        return Ok(issue);
    }

    [HttpPut("{issueId:long}/transition")]
    [RequirePermission("ISSUE_TRANSITION")]
    public async Task<IActionResult> Transition(
        long issueId,
        [FromBody] TransitionIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var issue = await _issueService.TransitionAsync(
            _tenantContext.TenantId,
            _tenantContext.UserId,
            issueId,
            request,
            cancellationToken);

        if (issue is null)
        {
            return NotFound();
        }

        return Ok(issue);
    }

    [HttpGet("search")]
    [RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Search(
        [FromQuery] IssueSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var effectiveRequest = request;
        if (request.AssignedOnly == true && !request.AssigneeUserId.HasValue)
        {
            effectiveRequest = new IssueSearchRequest
            {
                Label = request.Label,
                ComponentId = request.ComponentId,
                FixVersionId = request.FixVersionId,
                AffectsVersionId = request.AffectsVersionId,
                RootOnly = request.RootOnly,
                ProjectId = request.ProjectId,
                ParentIssueId = request.ParentIssueId,
                DueFrom = request.DueFrom,
                DueTo = request.DueTo,
                WorkGroup = request.WorkGroup,
                Today = request.Today,
                IssueKey = request.IssueKey,
                IssueType = request.IssueType,
                Status = request.Status,
                Priority = request.Priority,
                AssigneeUserId = _tenantContext.UserId,
                AssignedOnly = request.AssignedOnly,
                ReporterUserId = request.ReporterUserId,
                Text = request.Text,
                Page = request.Page,
                PageSize = request.PageSize,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection
            };
        }

        var issues = await _issueService.SearchAsync(
            _tenantContext.TenantId,
            effectiveRequest,
            cancellationToken);

        return Ok(issues);
    }

    [HttpDelete("{issueId:long}")]
    [RequirePermission("ISSUE_DELETE")]
    public async Task<IActionResult> Delete(
        long issueId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var issue = await _issueService.DeleteAsync(
            _tenantContext.TenantId,
            _tenantContext.UserId,
            issueId,
            cancellationToken);

        if (issue is null)
        {
            return NotFound();
        }

        return Ok(issue);
    }
}
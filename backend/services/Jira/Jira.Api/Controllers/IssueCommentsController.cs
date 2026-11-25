using Authorization;
using Jira.Application.Abstractions.Services;
using Jira.Contracts.Issues;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace Jira.Api.Controllers;

[ApiController]
[Route("api/issues/{issueId:long}/comments")]
public sealed class IssueCommentsController : ControllerBase
{
    private readonly IIssueCommentService _commentService;
    private readonly ITenantContext _tenantContext;

    public IssueCommentsController(
        IIssueCommentService commentService,
        ITenantContext tenantContext)
    {
        _commentService = commentService;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    [RequirePermission("ISSUE_COMMENT_CREATE")]
    public async Task<IActionResult> Create(
        long issueId,
        [FromBody] CreateIssueCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var comment =
            await _commentService.CreateAsync(
                _tenantContext.TenantId,
                _tenantContext.UserId,
                issueId,
                request,
                cancellationToken);

        return Ok(comment);
    }

    [HttpGet]
    [RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> GetByIssue(
        long issueId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return Unauthorized();
        }

        var comments =
            await _commentService.GetByIssueAsync(
                _tenantContext.TenantId,
                issueId,
                cancellationToken);

        return Ok(comments);
    }
}
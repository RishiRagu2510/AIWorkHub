using Authorization;
using Jira.Api.Collaboration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jira.Api.Controllers;


[ApiController, Authorize, Route("api/issues/{issueId:long}/attachments")]
public sealed class AttachmentsController(AttachmentService attachments) : ControllerBase
{
    [HttpGet, RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> List(long issueId, CancellationToken ct)
    {
        var items = await attachments.List(issueId, ct);
        return items is null ? NotFound() : Ok(items);
    }

    [HttpPost, RequirePermission("ISSUE_COMMENT_CREATE"), RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(long issueId, IFormFile file, CancellationToken ct)
    {
        var result = await attachments.Upload(issueId, file.FileName, file.Length, file.OpenReadStream, ct);
        return result.Outcome switch
        {
            AttachmentOutcome.Ok => Ok(result.Attachment),
            AttachmentOutcome.InvalidFile => BadRequest(new { message = result.Message }),
            AttachmentOutcome.InvalidName => BadRequest(),
            AttachmentOutcome.StorageUnavailable => StatusCode(503, new { message = result.Message }),
            _ => NotFound()
        };
    }

    [HttpDelete("{id:guid}"), RequirePermission("ISSUE_COMMENT_CREATE")]
    public async Task<IActionResult> Delete(long issueId, Guid id, CancellationToken ct) =>
        await attachments.Delete(issueId, id, ct) switch
        {
            AttachmentOutcome.Ok => NoContent(),
            AttachmentOutcome.NotAllowed => Forbid(),
            _ => NotFound()
        };

    [HttpGet("{id:guid}/download"), RequirePermission("ISSUE_VIEW")]
    public async Task<IActionResult> Download(long issueId, Guid id, CancellationToken ct)
    {
        var result = await attachments.Download(issueId, id, ct);
        switch (result.Outcome)
        {
            case AttachmentOutcome.Ok:
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.Headers.CacheControl = "no-store";
                return File(result.Data!, "application/octet-stream", result.FileName);
            case AttachmentOutcome.StorageUnavailable:
                return StatusCode(503, new { message = result.Message });
            default:
                return NotFound();
        }
    }
}

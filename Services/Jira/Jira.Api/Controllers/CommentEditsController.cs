using Authorization;
using Jira.Api.Collaboration;
using Jira.Contracts.Collaboration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jira.Api.Controllers;


[ApiController, Authorize, RequirePermission("ISSUE_COMMENT_CREATE"), Route("api/issues/{issueId:long}/comments/{commentId:long}")]
public sealed class CommentEditsController(CommentEditService comments) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Edit(long issueId, long commentId, EditCommentRequest request, CancellationToken ct) =>
        Reply(await comments.Edit(issueId, commentId, request.Body, ct));

    [HttpDelete]
    public async Task<IActionResult> Delete(long issueId, long commentId, CancellationToken ct) =>
        Reply(await comments.Delete(issueId, commentId, ct));

    private IActionResult Reply(CommentEditResult result) => result.Outcome switch
    {
        CommentEditOutcome.Done => result.Comment is null ? NoContent() : Ok(result.Comment),
        CommentEditOutcome.InvalidBody => BadRequest(),
        CommentEditOutcome.NotAllowed => Forbid(),
        _ => NotFound()
    };
}

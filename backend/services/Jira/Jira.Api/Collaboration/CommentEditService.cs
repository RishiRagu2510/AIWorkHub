using Jira.Api.Controllers;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Services;
using Jira.Contracts.Collaboration;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Jira.Api.Collaboration;

public enum CommentEditOutcome { Done, InvalidBody, IssueNotFound, CommentNotFound, NotAllowed }

public sealed record CommentEditResult(CommentEditOutcome Outcome, EditedCommentDto? Comment = null);


public sealed class CommentEditService(JiraDbContext db, JiraResourceScope scope, ITenantContext context, WritePrecondition precondition, IOutboxWriter audit)
{
    public const int MaxBodyLength = 20000;

    public Task<CommentEditResult> Edit(long issueId, long commentId, string body, CancellationToken ct) => Change(issueId, commentId, body, delete: false, ct);

    public Task<CommentEditResult> Delete(long issueId, long commentId, CancellationToken ct) => Change(issueId, commentId, null, delete: true, ct);

    private async Task<CommentEditResult> Change(long issueId, long commentId, string? body, bool delete, CancellationToken ct)
    {
        if (!delete && (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)) return new(CommentEditOutcome.InvalidBody);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-context.TenantId})", ct);
        var issue = await scope.Issues(context.TenantId).SingleOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null) return new(CommentEditOutcome.IssueNotFound);
        var comment = await scope.Comments(context.TenantId).SingleOrDefaultAsync(c => c.Id == commentId && c.IssueId == issueId, ct);
        if (comment is null) return new(CommentEditOutcome.CommentNotFound);
        if (comment.AuthorUserId != context.UserId && !context.Roles.Contains("ADMIN")) return new(CommentEditOutcome.NotAllowed);
        precondition.Check(comment.Version);
        var before = new { commentId, comment.Body };
        if (delete) db.Remove(comment); else comment.UpdateBody(body!);
        await db.SaveChangesAsync(ct);
        await CollaborationAudit.Write(audit, issue, context.UserId, delete ? "ISSUE_COMMENT_DELETED" : "ISSUE_COMMENT_UPDATED", before, delete ? null : new { commentId, comment.Body }, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(CommentEditOutcome.Done, delete ? null : new EditedCommentDto(comment.Id, comment.Body, comment.AuthorUserId, comment.CreatedAt, comment.UpdatedAt, comment.Version));
    }
}

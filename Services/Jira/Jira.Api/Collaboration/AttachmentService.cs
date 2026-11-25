using Jira.Api.Controllers;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Services;
using Jira.Contracts.Collaboration;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Jira.Api.Collaboration;

public enum AttachmentOutcome { Ok, InvalidFile, InvalidName, IssueNotFound, AttachmentNotFound, NotAllowed, StorageUnavailable }

public sealed record UploadResult(AttachmentOutcome Outcome, AttachmentDto? Attachment = null, string? Message = null);

public sealed record DownloadResult(AttachmentOutcome Outcome, byte[]? Data = null, string? FileName = null, string? Message = null);





public sealed class AttachmentService(JiraDbContext db, JiraResourceScope scope, ITenantContext context, IAttachmentStorage storage, IOutboxWriter audit)
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private string ObjectKey(Guid id) => $"{context.TenantId}/{id:N}";

    private static AttachmentDto ToDto(AttachmentRecord a) => new(a.Id, a.TenantId, a.IssueId, a.AuthorUserId, a.Name, a.Size, a.CreatedAt);

    
    public async Task<IReadOnlyList<AttachmentDto>?> List(long issueId, CancellationToken ct)
    {
        if (!await scope.Issues(context.TenantId).AnyAsync(i => i.Id == issueId, ct)) return null;
        var items = await db.Set<AttachmentRecord>().AsNoTracking().Where(a => a.TenantId == context.TenantId && a.IssueId == issueId).OrderBy(a => a.CreatedAt).ToArrayAsync(ct);
        return items.Select(ToDto).ToArray();
    }

    public async Task<UploadResult> Upload(long issueId, string fileName, long length, Func<Stream> open, CancellationToken ct)
    {
        if (length is <= 0 or > MaxBytes) return new(AttachmentOutcome.InvalidFile, Message: "Choose a file between 1 byte and 10 MB.");
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl)) return new(AttachmentOutcome.InvalidName);
        if (!await scope.Issues(context.TenantId).AnyAsync(i => i.Id == issueId, ct)) return new(AttachmentOutcome.IssueNotFound);

        var item = new AttachmentRecord { Id = Guid.NewGuid(), TenantId = context.TenantId, IssueId = issueId, AuthorUserId = context.UserId, Name = name, Size = length, CreatedAt = DateTimeOffset.UtcNow };
        try { using var stream = open(); await storage.Put(ObjectKey(item.Id), stream, length, ct); }
        catch { return new(AttachmentOutcome.StorageUnavailable, Message: "Attachment storage is unavailable."); }
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-context.TenantId})", ct);
            var issue = await scope.Issues(context.TenantId).SingleOrDefaultAsync(i => i.Id == issueId, ct);
            if (issue is null) throw new ResourceNotFoundException();
            db.Add(item);
            await db.SaveChangesAsync(ct);
            await CollaborationAudit.Write(audit, issue, context.UserId, "ISSUE_ATTACHMENT_ADDED", null, new { attachmentId = item.Id, item.Name, item.Size }, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await storage.Remove(ObjectKey(item.Id), CancellationToken.None); } catch {  }
            throw;
        }
        return new(AttachmentOutcome.Ok, ToDto(item));
    }

    
    public async Task<AttachmentOutcome> Delete(long issueId, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-context.TenantId})", ct);
        var issue = await scope.Issues(context.TenantId).SingleOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null) return AttachmentOutcome.IssueNotFound;
        var item = await db.Set<AttachmentRecord>().SingleOrDefaultAsync(a => a.Id == id && a.IssueId == issueId && a.TenantId == context.TenantId, ct);
        if (item is null) return AttachmentOutcome.AttachmentNotFound;
        if (item.AuthorUserId != context.UserId && !context.Roles.Contains("ADMIN")) return AttachmentOutcome.NotAllowed;
        db.Remove(item);
        await CollaborationAudit.Write(audit, issue, context.UserId, "ISSUE_ATTACHMENT_DELETED", new { attachmentId = id, item.Name, item.Size }, null, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        try { await storage.Remove(ObjectKey(id), ct); } catch {  }
        return AttachmentOutcome.Ok;
    }

    public async Task<DownloadResult> Download(long issueId, Guid id, CancellationToken ct)
    {
        if (!await scope.Issues(context.TenantId).AnyAsync(i => i.Id == issueId, ct)) return new(AttachmentOutcome.IssueNotFound);
        var item = await db.Set<AttachmentRecord>().SingleOrDefaultAsync(a => a.Id == id && a.IssueId == issueId && a.TenantId == context.TenantId, ct);
        if (item is null) return new(AttachmentOutcome.AttachmentNotFound);
        try { return new(AttachmentOutcome.Ok, await storage.Get(ObjectKey(id), ct), item.Name); }
        catch { return new(AttachmentOutcome.StorageUnavailable, Message: "Attachment storage is unavailable."); }
    }
}

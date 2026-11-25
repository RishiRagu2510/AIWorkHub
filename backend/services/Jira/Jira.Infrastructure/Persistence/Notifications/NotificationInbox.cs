using Jira.Application.Services;
using Microsoft.EntityFrameworkCore;
using TenantContext;
namespace Jira.Infrastructure.Persistence.Notifications;
public sealed record NotificationDto(Guid Id, string Kind, long IssueId, string IssueKey, string IssueSummary,
    long ActorUserId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, long? TimesheetPeriodId = null, string? RejectionReason = null, DateOnly? TimesheetPeriodStart = null, long? TimesheetUserId = null, string? IssueStatus = null);
public sealed record NotificationPage(IReadOnlyList<NotificationDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed class NotificationInbox(JiraDbContext db, JiraResourceScope scope, ITenantContext context)
{
    private IQueryable<Notification> Visible(bool canReview = false)
    {
        var issues = scope.Issues(context.TenantId);
        return db.Notifications.Where(n => context.IsAuthenticated && n.TenantId == context.TenantId &&
            n.RecipientUserId == context.UserId && (n.TimesheetPeriodId == null && issues.Any(i => i.Id == n.IssueId) || n.TimesheetPeriodId != null && db.TimesheetPeriods.Any(p=>p.Id==n.TimesheetPeriodId && p.TenantId==context.TenantId &&
              (p.UserId==context.UserId || canReview && !db.WorkLogs.Any(w=>w.PeriodId==p.Id&&!issues.Any(i=>i.Id==w.IssueId))))));
    }
    public async Task<NotificationPage> ListAsync(int page, int pageSize, bool unreadOnly, CancellationToken ct, bool canReview = false)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
            throw new ArgumentException("Use page between 1 and 100000 and pageSize between 1 and 100.");
        var query = Visible(canReview);
        if (unreadOnly) query = query.Where(n => n.ReadAt == null);
        var count = await query.CountAsync(ct);
        var items = await (from n in query
            join issue in scope.Issues(context.TenantId) on n.IssueId equals issue.Id into issueGroup
            from i in issueGroup.DefaultIfEmpty()
            join period in db.TimesheetPeriods on n.TimesheetPeriodId equals (long?)period.Id into periodGroup
            from p in periodGroup.DefaultIfEmpty()
            orderby n.CreatedAt descending, n.Id descending
            select new NotificationDto(n.Id, n.Kind, n.IssueId, i == null ? "Timesheet" : i.IssueKey, i == null ? p.Status : i.Summary, n.ActorUserId, n.CreatedAt, n.ReadAt,n.TimesheetPeriodId,n.Reason,p == null ? null : p.PeriodStart,p == null ? null : p.UserId, i == null ? null : i.StatusCode))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }
    public Task<int> CountAsync(CancellationToken ct, bool canReview = false) => Visible(canReview).CountAsync(n => n.ReadAt == null, ct);
    public async Task ReadAsync(Guid id, CancellationToken ct, bool canReview = false)
    {
        var query = Visible(canReview).Where(n => n.Id == id);
        if (!await query.AnyAsync(ct)) throw new ResourceNotFoundException();
        await query.Where(n => n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
    }
    public Task<int> ReadAllAsync(CancellationToken ct, bool canReview = false)
        => Visible(canReview).Where(n => n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
}

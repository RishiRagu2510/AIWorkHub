using Jira.Application.Abstractions.Services;
using Jira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Jira.Infrastructure.Persistence.Notifications;
public sealed class NotificationWriter(JiraDbContext db) : INotificationWriter
{
    public Task OnboardingAsync(Issue issue, long actorUserId, Guid eventId, string kind, long[] recipients, CancellationToken ct)
    {
        if (kind is not ("EMPLOYEE_ONBOARDING_REQUESTED" or "EMPLOYEE_ONBOARDING_COMPLETED")) throw new ArgumentException("Invalid onboarding notification.");
        return WriteAsync(issue, actorUserId, eventId, kind, recipients.Select(id => (long?)id).ToArray(), ct, includeActor: true);
    }
    public Task AssignmentAsync(Issue issue, long actorUserId, Guid eventId, CancellationToken ct, IReadOnlyCollection<long>? alreadyAssigned = null)
    {
        var assignees = issue.Assignees.Select(a => (long?)a.UserId).ToList();
        if (assignees.Count == 0 && issue.AssigneeUserId.HasValue) assignees.Add(issue.AssigneeUserId);
        
        var recipients = assignees.Where(id => alreadyAssigned is null || !alreadyAssigned.Contains(id!.Value)).ToArray();
        return WriteAsync(issue, actorUserId, eventId, "ISSUE_ASSIGNED", recipients, ct);
    }
    public async Task CommentAsync(Issue issue, long actorUserId, Guid eventId, CancellationToken ct, long[]? mentions = null)
    {
        await WriteAsync(issue, actorUserId, eventId, "ISSUE_MENTIONED", (mentions ?? []).Select(id => (long?)id).ToArray(), ct);
        var commentRecipients = issue.Assignees.Select(a => (long?)a.UserId).ToList();
        if (commentRecipients.Count == 0 && issue.AssigneeUserId.HasValue) commentRecipients.Add(issue.AssigneeUserId);
        commentRecipients.Add(issue.ReporterUserId);
        await WriteAsync(issue, actorUserId, eventId, "ISSUE_COMMENT_ADDED", commentRecipients.ToArray(), ct);
    }

    private async Task WriteAsync(Issue issue, long actor, Guid eventId, string kind, long?[] recipients, CancellationToken ct, bool includeActor = false)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Notifications must be written within the business transaction.");
        foreach (var recipient in recipients.Where(id => id.HasValue && id > 0 && (includeActor || id != actor)).Select(id => id!.Value).Distinct())
        {
            var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications (id, event_id, tenant_id, recipient_user_id, actor_user_id, issue_id, kind, created_at)
                VALUES ({id}, {eventId}, {issue.TenantId}, {recipient}, {actor}, {issue.Id}, {kind}, {now})
                ON CONFLICT (tenant_id, event_id, recipient_user_id) DO NOTHING
                """, ct);
        }
    }
}

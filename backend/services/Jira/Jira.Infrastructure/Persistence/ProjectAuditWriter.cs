using System.Text.Json;
using Jira.Application.Abstractions.Persistence;
using Jira.Domain.Entities;
using Messaging.Events;
using TenantContext;

namespace Jira.Infrastructure.Persistence;

public sealed class ProjectAuditWriter(IOutboxWriter outbox, ITenantContext context)
{
    public static object Snapshot(Project project) => new
    {
        project.Id, project.Key, project.Name, project.AccountId, AccountName = project.Name, ProjectName = project.DisplayName, project.Description,
        ProjectType = project.ProjectType.ToString().ToUpperInvariant(), Status = project.Status.ToString().ToUpperInvariant(), project.ParentProjectId,
        project.ProjectLeadUserId, project.Version
    };

    public Task RecordAsync(Project project, string action, object? before, object? after, CancellationToken ct)
    {
        if (!context.IsAuthenticated || context.TenantId != project.TenantId || context.UserId <= 0)
            throw new InvalidOperationException("An authenticated project audit actor is required.");
        
        
        var message = new IssueAuditEvent
        {
            EventId = Guid.NewGuid(), EventType = action, TenantId = project.TenantId,
            EntityType = "PROJECT", EntityId = project.Id, EntityKey = project.Key,
            PerformedByUserId = context.UserId, OccurredAt = DateTimeOffset.UtcNow,
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after)
        };
        return outbox.AddAsync(message.EventId, action, "aiworkhub.issue.audit", JsonSerializer.Serialize(message), message.OccurredAt, ct);
    }
}

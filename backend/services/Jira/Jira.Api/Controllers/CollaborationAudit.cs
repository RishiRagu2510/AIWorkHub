using System.Text.Json;
using Jira.Application.Abstractions.Persistence;
using Jira.Domain.Entities;
using Messaging.Events;
namespace Jira.Api.Controllers;
public static class CollaborationAudit
{
    public static Task Write(IOutboxWriter writer, Issue issue, long actor, string action, object? before, object? after, CancellationToken ct) {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var payload = new IssueAuditEvent { EventId = id, EventType = action, TenantId = issue.TenantId, EntityType = "ISSUE", EntityId = issue.Id, EntityKey = issue.IssueKey, PerformedByUserId = actor, OldValues = before is null ? null : JsonSerializer.Serialize(before), NewValues = after is null ? null : JsonSerializer.Serialize(after), Metadata = "{}", OccurredAt = now };
        return writer.AddAsync(id, action, "aiworkhub.issue.audit", JsonSerializer.Serialize(payload), now, ct);
    }
}

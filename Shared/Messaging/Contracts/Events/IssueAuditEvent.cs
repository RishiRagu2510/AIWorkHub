namespace Messaging.Events;

public sealed class IssueAuditEvent
{
    public Guid EventId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public long TenantId { get; init; }

    public string EntityType { get; init; } = "ISSUE";

    public long EntityId { get; init; }

    public string EntityKey { get; init; } = string.Empty;

    public long PerformedByUserId { get; init; }

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public string? Metadata { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

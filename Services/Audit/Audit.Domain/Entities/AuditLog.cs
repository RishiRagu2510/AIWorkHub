namespace Audit.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        Guid? eventId,
        long? tenantId,
        long? actorUserId,
        string action,
        string? entityType,
        long? entityId,
        string? source,
        string? oldValues,
        string? newValues,
        string metadata,
        DateTimeOffset occurredAt)
    {
        EventId = eventId;
        TenantId = tenantId;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Source = source;
        OldValues = oldValues;
        NewValues = newValues;
        Metadata = metadata;
        OccurredAt = occurredAt;
    }

    public long Id { get; private set; }

    public Guid? EventId { get; private set; }

    public long? TenantId { get; private set; }

    public long? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string? EntityType { get; private set; }

    public long? EntityId { get; private set; }

    public string? Source { get; private set; }

    public string? OldValues { get; private set; }

    public string? NewValues { get; private set; }

    public string Metadata { get; private set; } = "{}";

    public DateTimeOffset OccurredAt { get; private set; }
}

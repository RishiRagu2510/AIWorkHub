namespace Audit.Contracts;

public sealed class AuditLogDto
{
    public long Id { get; init; }

    public Guid? EventId { get; init; }

    public long? TenantId { get; init; }

    public long? ActorUserId { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? EntityType { get; init; }

    public long? EntityId { get; init; }

    public string? Source { get; init; }

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public string Metadata { get; init; } = "{}";

    public DateTimeOffset OccurredAt { get; init; }
}
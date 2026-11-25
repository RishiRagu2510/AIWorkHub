namespace Audit.Contracts;

public sealed class AuditLogQuery
{
    public string? EntityType { get; init; }

    public long? EntityId { get; init; }

    public string? Action { get; init; }

    public long? ActorUserId { get; init; }

    public string? Source { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
namespace Audit.Contracts;

public sealed class IssueCreatedEvent
{
    public string EventType { get; init; } =
        "ISSUE_CREATED";

    public long TenantId { get; init; }

    public string EntityType { get; init; } =
        "ISSUE";

    public long EntityId { get; init; }

    public string EntityKey { get; init; } =
        string.Empty;

    public long PerformedByUserId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
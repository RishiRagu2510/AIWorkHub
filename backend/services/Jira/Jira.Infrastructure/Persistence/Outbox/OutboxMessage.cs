namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public string? LockedBy { get; set; }
}
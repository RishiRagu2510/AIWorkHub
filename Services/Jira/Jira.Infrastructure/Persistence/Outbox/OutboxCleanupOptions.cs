namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxCleanupOptions
{
    public int RetentionDays { get; set; } = 7;

    public int BatchSize { get; set; } = 500;

    public TimeSpan RunInterval { get; set; } =
        TimeSpan.FromHours(1);
}
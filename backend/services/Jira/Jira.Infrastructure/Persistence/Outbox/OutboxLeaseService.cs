using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxLeaseService
{
    private readonly JiraDbContext _dbContext;

    public OutboxLeaseService(
        JiraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryClaimAsync(
        long messageId,
        string publisherInstanceId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (messageId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(publisherInstanceId))
        {
            throw new ArgumentException(
                "Publisher instance ID is required.",
                nameof(publisherInstanceId));
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                "Lease duration must be greater than zero.");
        }

        var leaseUntil =
            DateTimeOffset.UtcNow + leaseDuration;

        var affectedRows =
            await _dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE outbox_messages
                    SET
                        locked_until = {leaseUntil},
                        locked_by = {publisherInstanceId}
                    WHERE
                        id = {messageId}
                        AND processed_at IS NULL
                        AND (
                            next_attempt_at IS NULL
                            OR next_attempt_at <= NOW()
                        )
                        AND (
                            locked_until IS NULL
                            OR locked_until <= NOW()
                        );
                    """,
                    cancellationToken);

        return affectedRows == 1;
    }

    public async Task<int> MarkPublishedAsync(
        long messageId,
        string publisherInstanceId,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE outbox_messages
                SET
                    processed_at = {processedAt},
                    next_attempt_at = NULL,
                    last_error = NULL,
                    attempt_count = attempt_count + 1,
                    locked_until = NULL,
                    locked_by = NULL
                WHERE
                    id = {messageId}
                    AND processed_at IS NULL
                    AND locked_by = {publisherInstanceId};
                """,
                cancellationToken);
    }

    public async Task<int> MarkFailedAsync(
        long messageId,
        string publisherInstanceId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE outbox_messages
                SET
                    attempt_count = {attemptCount},
                    last_error = {lastError},
                    next_attempt_at = {nextAttemptAt},
                    locked_until = NULL,
                    locked_by = NULL
                WHERE
                    id = {messageId}
                    AND processed_at IS NULL
                    AND locked_by = {publisherInstanceId};
                """,
                cancellationToken);
    }
}

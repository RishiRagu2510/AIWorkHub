using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxCleanupService
{
    private readonly JiraDbContext _dbContext;

    public OutboxCleanupService(
        JiraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> CleanupAsync(
        int retentionDays,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (retentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionDays),
                "Retention days must be greater than zero.");
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                "Batch size must be greater than zero.");
        }

        var cutoff =
            DateTimeOffset.UtcNow.AddDays(-retentionDays);

        var totalDeleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            var ids = await _dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message =>
                    message.ProcessedAt != null &&
                    message.ProcessedAt < cutoff &&
                    (message.LockedUntil == null ||
                     message.LockedUntil <= now))
                .OrderBy(message => message.Id)
                .Take(batchSize)
                .Select(message => message.Id)
                .ToListAsync(cancellationToken);

            if (ids.Count == 0)
            {
                break;
            }

            var deleted =
                await _dbContext.OutboxMessages
                    .Where(message =>
                        ids.Contains(message.Id))
                    .ExecuteDeleteAsync(
                        cancellationToken);

            totalDeleted += deleted;

            if (deleted < ids.Count)
            {
                break;
            }
        }

        return totalDeleted;
    }
}
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Jira.Infrastructure.Tests;

public sealed class OutboxCleanupTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("jira_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private DbContextOptions<JiraDbContext> _dbOptions = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _dbOptions =
            new DbContextOptionsBuilder<JiraDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options;

        await using var dbContext =
            new JiraDbContext(_dbOptions);

        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private async Task ResetDatabaseAsync()
    {
        await using var dbContext =
            new JiraDbContext(_dbOptions);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE outbox_messages RESTART IDENTITY;");
    }

    [Fact]
    public async Task CleanupAsync_DeletesProcessedMessageOlderThanRetention()
    {
        await ResetDatabaseAsync();

        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "CLEANUP_TEST",
                Topic = "aiworkhub.issue.audit",
                Payload = """{"cleanupTest":true}""",
                OccurredAt =
                    DateTimeOffset.UtcNow.AddDays(-8),
                ProcessedAt =
                    DateTimeOffset.UtcNow.AddDays(-8),
                NextAttemptAt = null,
                AttemptCount = 1,
                LastError = null,
                LockedUntil = null,
                LockedBy = null
            };

            setupContext.OutboxMessages.Add(
                outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var cleanupService =
            new OutboxCleanupService(context);

        var deleted =
            await cleanupService.CleanupAsync(
                retentionDays: 7,
                batchSize: 500);

        Assert.Equal(1, deleted);

        var remaining =
            await context.OutboxMessages
                .SingleOrDefaultAsync(
                    message => message.Id == messageId);

        Assert.Null(remaining);
    }

    [Fact]
    public async Task CleanupAsync_PreservesRecentProcessedMessage()
    {
        await ResetDatabaseAsync();

        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "CLEANUP_TEST",
                Topic = "aiworkhub.issue.audit",
                Payload = """{"cleanupTest":true}""",
                OccurredAt =
                    DateTimeOffset.UtcNow.AddDays(-1),
                ProcessedAt =
                    DateTimeOffset.UtcNow.AddDays(-1),
                NextAttemptAt = null,
                AttemptCount = 1,
                LastError = null,
                LockedUntil = null,
                LockedBy = null
            };

            setupContext.OutboxMessages.Add(
                outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var cleanupService =
            new OutboxCleanupService(context);

        var deleted =
            await cleanupService.CleanupAsync(
                retentionDays: 7,
                batchSize: 500);

        Assert.Equal(0, deleted);

        var remaining =
            await context.OutboxMessages
                .SingleOrDefaultAsync(
                    message => message.Id == messageId);

        Assert.NotNull(remaining);
    }

    [Fact]
    public async Task CleanupAsync_PreservesPendingMessage()
    {
        await ResetDatabaseAsync();

        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "CLEANUP_TEST",
                Topic = "aiworkhub.issue.audit",
                Payload = """{"cleanupTest":true}""",
                OccurredAt =
                    DateTimeOffset.UtcNow.AddDays(-8),
                ProcessedAt = null,
                NextAttemptAt = null,
                AttemptCount = 0,
                LastError = null,
                LockedUntil = null,
                LockedBy = null
            };

            setupContext.OutboxMessages.Add(
                outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var cleanupService =
            new OutboxCleanupService(context);

        var deleted =
            await cleanupService.CleanupAsync(
                retentionDays: 7,
                batchSize: 500);

        Assert.Equal(0, deleted);

        var remaining =
            await context.OutboxMessages
                .SingleOrDefaultAsync(
                    message => message.Id == messageId);

        Assert.NotNull(remaining);
        Assert.Null(remaining.ProcessedAt);
    }

    [Fact]
    public async Task CleanupAsync_PreservesMessageWithActiveLease()
    {
        await ResetDatabaseAsync();

        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "CLEANUP_TEST",
                Topic = "aiworkhub.issue.audit",
                Payload = """{"cleanupTest":true}""",
                OccurredAt =
                    DateTimeOffset.UtcNow.AddDays(-8),
                ProcessedAt =
                    DateTimeOffset.UtcNow.AddDays(-8),
                NextAttemptAt = null,
                AttemptCount = 1,
                LastError = null,
                LockedUntil =
                    DateTimeOffset.UtcNow.AddMinutes(5),
                LockedBy = "publisher-a"
            };

            setupContext.OutboxMessages.Add(
                outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var cleanupService =
            new OutboxCleanupService(context);

        var deleted =
            await cleanupService.CleanupAsync(
                retentionDays: 7,
                batchSize: 500);

        Assert.Equal(0, deleted);

        var remaining =
            await context.OutboxMessages
                .SingleOrDefaultAsync(
                    message => message.Id == messageId);

        Assert.NotNull(remaining);
        Assert.NotNull(remaining.ProcessedAt);
        Assert.Equal(
            "publisher-a",
            remaining.LockedBy);
        Assert.NotNull(remaining.LockedUntil);
    }

    [Fact]
    public async Task CleanupAsync_DeletesExpiredLease()
    {
        await ResetDatabaseAsync();

        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "CLEANUP_TEST",
                Topic = "aiworkhub.issue.audit",
                Payload = """{"cleanupTest":true}""",
                OccurredAt =
                    DateTimeOffset.UtcNow.AddDays(-8),
                ProcessedAt =
                    DateTimeOffset.UtcNow.AddDays(-8),
                NextAttemptAt = null,
                AttemptCount = 1,
                LastError = null,
                LockedUntil =
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                LockedBy = "expired-publisher"
            };

            setupContext.OutboxMessages.Add(
                outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var cleanupService =
            new OutboxCleanupService(context);

        var deleted =
            await cleanupService.CleanupAsync(
                retentionDays: 7,
                batchSize: 500);

        Assert.Equal(1, deleted);

        var remaining =
            await context.OutboxMessages
                .SingleOrDefaultAsync(
                    message => message.Id == messageId);

        Assert.Null(remaining);
    }
}
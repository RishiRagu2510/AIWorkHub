using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Jira.Infrastructure.Tests;

public sealed class OutboxLeaseTests : IAsyncLifetime
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

    [Fact]
    public async Task TryClaimAsync_AllowsOnlyOnePublisherToClaimMessage()
    {
        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "ISSUE_CREATED",
                Topic = "aiworkhub.issue.audit",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ProcessedAt = null,
                NextAttemptAt = null,
                AttemptCount = 0,
                LastError = null,
                LockedUntil = null,
                LockedBy = null
            };

            setupContext.OutboxMessages.Add(outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var contextA =
            new JiraDbContext(_dbOptions);

        await using var contextB =
            new JiraDbContext(_dbOptions);

        var leaseA =
            new OutboxLeaseService(contextA);

        var leaseB =
            new OutboxLeaseService(contextB);

        var results = await Task.WhenAll(
            leaseA.TryClaimAsync(
                messageId,
                "publisher-a",
                TimeSpan.FromMinutes(2)),

            leaseB.TryClaimAsync(
                messageId,
                "publisher-b",
                TimeSpan.FromMinutes(2)));

        Assert.Equal(
            1,
            results.Count(result => result));

        Assert.Equal(
            1,
            results.Count(result => !result));

        await using var verificationContext =
            new JiraDbContext(_dbOptions);

        var claimedMessage =
            await verificationContext.OutboxMessages
                .SingleAsync(
                    item => item.Id == messageId);

        Assert.Null(claimedMessage.ProcessedAt);
        Assert.NotNull(claimedMessage.LockedUntil);
        Assert.NotNull(claimedMessage.LockedBy);

        Assert.Contains(
            claimedMessage.LockedBy!,
            new[]
            {
                "publisher-a",
                "publisher-b"
            });
    }

    [Fact]
    public async Task TryClaimAsync_CanReclaimExpiredLease()
    {
        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "ISSUE_CREATED",
                Topic = "aiworkhub.issue.audit",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ProcessedAt = null,
                NextAttemptAt = null,
                AttemptCount = 0,
                LastError = null,
                LockedUntil =
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                LockedBy = "expired-publisher"
            };

            setupContext.OutboxMessages.Add(outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var leaseService =
            new OutboxLeaseService(context);

        var claimed =
            await leaseService.TryClaimAsync(
                messageId,
                "new-publisher",
                TimeSpan.FromMinutes(2));

        Assert.True(claimed);

        var storedMessage =
            await context.OutboxMessages
                .SingleAsync(
                    item => item.Id == messageId);

        Assert.Equal(
            "new-publisher",
            storedMessage.LockedBy);

        Assert.NotNull(
            storedMessage.LockedUntil);

        Assert.True(
            storedMessage.LockedUntil >
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task TryClaimAsync_DoesNotClaimProcessedMessage()
    {
        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "ISSUE_CREATED",
                Topic = "aiworkhub.issue.audit",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ProcessedAt = DateTimeOffset.UtcNow,
                NextAttemptAt = null,
                AttemptCount = 1,
                LastError = null,
                LockedUntil = null,
                LockedBy = null
            };

            setupContext.OutboxMessages.Add(outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var leaseService =
            new OutboxLeaseService(context);

        var claimed =
            await leaseService.TryClaimAsync(
                messageId,
                "publisher-a",
                TimeSpan.FromMinutes(2));

        Assert.False(claimed);

        var storedMessage =
            await context.OutboxMessages
                .SingleAsync(
                    item => item.Id == messageId);

        Assert.NotNull(
            storedMessage.ProcessedAt);

        Assert.Null(
            storedMessage.LockedUntil);

        Assert.Null(
            storedMessage.LockedBy);
    }

    [Fact]
    public async Task MarkPublishedAsync_MarksMessageProcessedAndReleasesLease()
    {
        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "ISSUE_CREATED",
                Topic = "aiworkhub.issue.audit",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ProcessedAt = null,
                NextAttemptAt = null,
                AttemptCount = 0,
                LastError = null,
                LockedUntil =
                    DateTimeOffset.UtcNow.AddMinutes(2),
                LockedBy = "publisher-a"
            };

            setupContext.OutboxMessages.Add(outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var leaseService =
            new OutboxLeaseService(context);

        var processedAt =
            DateTimeOffset.UtcNow;

        var affectedRows =
            await leaseService.MarkPublishedAsync(
                messageId,
                "publisher-a",
                processedAt);

        Assert.Equal(
            1,
            affectedRows);

        var storedMessage =
            await context.OutboxMessages
                .SingleAsync(
                    item => item.Id == messageId);

        Assert.NotNull(
            storedMessage.ProcessedAt);

        Assert.Null(
            storedMessage.NextAttemptAt);

        Assert.Null(
            storedMessage.LastError);

        Assert.Equal(
            1,
            storedMessage.AttemptCount);

        Assert.Null(
            storedMessage.LockedUntil);

        Assert.Null(
            storedMessage.LockedBy);
    }

    [Fact]
    public async Task MarkPublishedAsync_DoesNotUpdateMessageOwnedByAnotherPublisher()
    {
        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "ISSUE_CREATED",
                Topic = "aiworkhub.issue.audit",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ProcessedAt = null,
                NextAttemptAt = null,
                AttemptCount = 0,
                LastError = null,
                LockedUntil =
                    DateTimeOffset.UtcNow.AddMinutes(2),
                LockedBy = "publisher-a"
            };

            setupContext.OutboxMessages.Add(outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var leaseService =
            new OutboxLeaseService(context);

        var affectedRows =
            await leaseService.MarkPublishedAsync(
                messageId,
                "publisher-b",
                DateTimeOffset.UtcNow);

        Assert.Equal(
            0,
            affectedRows);

        var storedMessage =
            await context.OutboxMessages
                .SingleAsync(
                    item => item.Id == messageId);

        Assert.Null(
            storedMessage.ProcessedAt);

        Assert.Equal(
            "publisher-a",
            storedMessage.LockedBy);
    }

    [Fact]
    public async Task MarkFailedAsync_SchedulesRetryAndReleasesLease()
    {
        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "ISSUE_CREATED",
                Topic = "aiworkhub.issue.audit",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ProcessedAt = null,
                NextAttemptAt = null,
                AttemptCount = 0,
                LastError = null,
                LockedUntil =
                    DateTimeOffset.UtcNow.AddMinutes(2),
                LockedBy = "publisher-a"
            };

            setupContext.OutboxMessages.Add(outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var leaseService =
            new OutboxLeaseService(context);

        var nextAttemptAt =
            DateTimeOffset.UtcNow.AddSeconds(30);

        const string error =
            "Kafka unavailable.";

        var affectedRows =
            await leaseService.MarkFailedAsync(
                messageId,
                "publisher-a",
                1,
                nextAttemptAt,
                error);

        Assert.Equal(
            1,
            affectedRows);

        var storedMessage =
            await context.OutboxMessages
                .SingleAsync(
                    item => item.Id == messageId);

        Assert.Null(
            storedMessage.ProcessedAt);

        Assert.Equal(
            1,
            storedMessage.AttemptCount);

        Assert.Equal(
            error,
            storedMessage.LastError);

        Assert.NotNull(
            storedMessage.NextAttemptAt);

        Assert.Null(
            storedMessage.LockedUntil);

        Assert.Null(
            storedMessage.LockedBy);
    }

    [Fact]
    public async Task MarkFailedAsync_DoesNotUpdateMessageOwnedByAnotherPublisher()
    {
        long messageId;

        await using (var setupContext =
                     new JiraDbContext(_dbOptions))
        {
            var outboxMessage = new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "ISSUE_CREATED",
                Topic = "aiworkhub.issue.audit",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                ProcessedAt = null,
                NextAttemptAt = null,
                AttemptCount = 0,
                LastError = null,
                LockedUntil =
                    DateTimeOffset.UtcNow.AddMinutes(2),
                LockedBy = "publisher-a"
            };

            setupContext.OutboxMessages.Add(outboxMessage);

            await setupContext.SaveChangesAsync();

            messageId = outboxMessage.Id;
        }

        await using var context =
            new JiraDbContext(_dbOptions);

        var leaseService =
            new OutboxLeaseService(context);

        var nextAttemptAt =
            DateTimeOffset.UtcNow.AddSeconds(30);

        var affectedRows =
            await leaseService.MarkFailedAsync(
                messageId,
                "publisher-b",
                1,
                nextAttemptAt,
                "Kafka unavailable.");

        Assert.Equal(
            0,
            affectedRows);

        var storedMessage =
            await context.OutboxMessages
                .SingleAsync(
                    item => item.Id == messageId);

        Assert.Null(
            storedMessage.ProcessedAt);

        Assert.Equal(
            0,
            storedMessage.AttemptCount);

        Assert.Null(
            storedMessage.NextAttemptAt);

        Assert.Null(
            storedMessage.LastError);

        Assert.Equal(
            "publisher-a",
            storedMessage.LockedBy);
    }
}

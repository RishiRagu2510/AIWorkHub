using System.Text.Json;
using Kafka;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxPublisherBackgroundService
    : BackgroundService
{
    private const int BatchSize = 50;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ErrorRetryInterval =
        TimeSpan.FromSeconds(2);

    private static readonly TimeSpan MaximumRetryDelay =
        TimeSpan.FromSeconds(60);

    private static readonly TimeSpan LeaseDuration =
        TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<OutboxPublisherBackgroundService>
        _logger;

    private readonly string _publisherInstanceId =
        $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Jira Outbox Publisher started. PublisherId: {PublisherId}",
            _publisherInstanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny =
                    await ProcessBatchAsync(stoppingToken);

                if (!processedAny)
                {
                    await Task.Delay(
                        PollInterval,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected error in Jira Outbox Publisher.");

                await Task.Delay(
                    ErrorRetryInterval,
                    stoppingToken);
            }
        }

        _logger.LogInformation(
            "Jira Outbox Publisher stopped. PublisherId: {PublisherId}",
            _publisherInstanceId);
    }

    private async Task<bool> ProcessBatchAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<JiraDbContext>();

        var kafkaProducer =
            scope.ServiceProvider
                .GetRequiredService<IKafkaProducer>();

        var leaseService =
            scope.ServiceProvider
                .GetRequiredService<OutboxLeaseService>();

        var now = DateTimeOffset.UtcNow;

        var messages = await dbContext.OutboxMessages
            .Where(message =>
                message.ProcessedAt == null &&
                (message.NextAttemptAt == null ||
                 message.NextAttemptAt <= now) &&
                (message.LockedUntil == null ||
                 message.LockedUntil <= now))
            .OrderBy(message => message.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return false;
        }

        var processedAny = false;

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var claimed =
                await leaseService.TryClaimAsync(
                    message.Id,
                    _publisherInstanceId,
                    LeaseDuration,
                    cancellationToken);

            if (!claimed)
            {
                _logger.LogInformation(
                    "Outbox message {OutboxMessageId} was already claimed by another publisher. PublisherId: {PublisherId}",
                    message.Id,
                    _publisherInstanceId);

                continue;
            }

            try
            {
                var auditEvent =
                    JsonSerializer.Deserialize<IssueAuditEvent>(
                        message.Payload);

                if (auditEvent is null)
                {
                    throw new InvalidOperationException(
                        $"Unable to deserialize outbox message {message.Id}.");
                }

                await kafkaProducer.PublishAsync(
                    message.Topic,
                    auditEvent,
                    cancellationToken);

                var attemptNumber =
                    message.AttemptCount + 1;

                var processedAt =
                    DateTimeOffset.UtcNow;

                var updatedRows =
                    await leaseService.MarkPublishedAsync(
                        message.Id,
                        _publisherInstanceId,
                        processedAt,
                        cancellationToken);

                if (updatedRows == 0)
                {
                    _logger.LogWarning(
                        "Outbox message {OutboxMessageId} was published to Kafka, but publisher {PublisherId} no longer owns the lease. EventId: {EventId}",
                        message.Id,
                        _publisherInstanceId,
                        message.EventId);

                    continue;
                }

                processedAny = true;

                _logger.LogInformation(
                    "Published outbox message {OutboxMessageId} event {EventId} type {EventType} on attempt {AttemptCount}. PublisherId: {PublisherId}",
                    message.Id,
                    message.EventId,
                    message.EventType,
                    attemptNumber,
                    _publisherInstanceId);
            }
            catch (Exception exception)
            {
                var attemptNumber =
                    message.AttemptCount + 1;

                var nextAttemptAt =
                    DateTimeOffset.UtcNow +
                    CalculateRetryDelay(attemptNumber);

                var lastError =
                    TruncateError(exception.Message);

                var updatedRows =
                    await leaseService.MarkFailedAsync(
                        message.Id,
                        _publisherInstanceId,
                        attemptNumber,
                        nextAttemptAt,
                        lastError,
                        cancellationToken);

                if (updatedRows > 0)
                {
                    processedAny = true;

                    _logger.LogWarning(
                        exception,
                        "Failed to publish outbox message {OutboxMessageId}. Attempt {AttemptCount}. Next retry at {NextAttemptAt}. PublisherId: {PublisherId}",
                        message.Id,
                        attemptNumber,
                        nextAttemptAt,
                        _publisherInstanceId);
                }
                else
                {
                    _logger.LogWarning(
                        exception,
                        "Failed to publish outbox message {OutboxMessageId}, but publisher {PublisherId} no longer owns the lease.",
                        message.Id,
                        _publisherInstanceId);
                }
            }
        }

        return processedAny;
    }

    private static TimeSpan CalculateRetryDelay(
        int attemptCount)
    {
        if (attemptCount <= 1)
        {
            return TimeSpan.Zero;
        }

        var exponent =
            Math.Min(attemptCount - 1, 6);

        var seconds =
            Math.Pow(2, exponent);

        var delay =
            TimeSpan.FromSeconds(seconds);

        return delay <= MaximumRetryDelay
            ? delay
            : MaximumRetryDelay;
    }

    private static string TruncateError(
        string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "Unknown outbox publishing error.";
        }

        const int maximumLength = 4000;

        return error.Length <= maximumLength
            ? error
            : error[..maximumLength];
    }
}

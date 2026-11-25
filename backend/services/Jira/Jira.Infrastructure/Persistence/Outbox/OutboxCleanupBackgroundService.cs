using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxCleanupBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxCleanupBackgroundService> _logger;
    private readonly OutboxCleanupOptions _options;

    public OutboxCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxCleanupBackgroundService> logger,
        IOptions<OutboxCleanupOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Jira Outbox Cleanup started. " +
            "RetentionDays: {RetentionDays}, " +
            "BatchSize: {BatchSize}, " +
            "RunInterval: {RunInterval}",
            _options.RetentionDays,
            _options.BatchSize,
            _options.RunInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);

                await Task.Delay(
                    _options.RunInterval,
                    stoppingToken);
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
                    "Unexpected error in Jira Outbox Cleanup.");

                await Task.Delay(
                    TimeSpan.FromMinutes(1),
                    stoppingToken);
            }
        }

        _logger.LogInformation(
            "Jira Outbox Cleanup stopped.");
    }

    private async Task RunCleanupAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var cleanupService =
            scope.ServiceProvider
                .GetRequiredService<OutboxCleanupService>();

        var deleted =
            await cleanupService.CleanupAsync(
                _options.RetentionDays,
                _options.BatchSize,
                cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Jira Outbox Cleanup deleted {DeletedCount} processed messages.",
                deleted);
        }
        else
        {
            _logger.LogDebug(
                "Jira Outbox Cleanup found no eligible messages.");
        }
    }
}
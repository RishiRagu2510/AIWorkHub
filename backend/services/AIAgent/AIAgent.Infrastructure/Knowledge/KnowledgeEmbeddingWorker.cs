using AIAgent.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIAgent.Infrastructure.Knowledge;

public sealed class KnowledgeEmbeddingWorker(IKnowledgeIndexer indexer, IKnowledgeEmbedder embedder, ILogger<KnowledgeEmbeddingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!embedder.Enabled) return;
        
        await Delay(TimeSpan.FromSeconds(10), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = TimeSpan.FromMinutes(2);
            try
            {
                var done = await indexer.IndexPendingAsync(100, stoppingToken);
                
                if (done >= 100) wait = TimeSpan.FromSeconds(3);
                else if (done > 0) wait = TimeSpan.FromSeconds(20);
                if (done > 0) logger.LogInformation("Company documents: added search-by-meaning vectors to {Count} passages.", done);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Company documents: could not add vectors this time ({Kind}); will try again.", ex.GetType().Name);
            }
            await Delay(wait, stoppingToken);
        }
    }

    private static async Task Delay(TimeSpan time, CancellationToken token)
    {
        try { await Task.Delay(time, token); }
        catch (OperationCanceledException) { }
    }
}

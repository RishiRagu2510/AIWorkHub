using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Audit.Infrastructure.Persistence;

public sealed class AuditRetentionService(IServiceScopeFactory scopes,IConfiguration config,ILogger<AuditRetentionService> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                var days=config.GetValue<int>("AuditRetention:Days");
                if(days>=30)
                {
                    await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AuditDbContext>();
                    var cutoff=DateTimeOffset.UtcNow.AddDays(-days);
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit_logs WHERE id IN (SELECT id FROM audit_logs WHERE occurred_at < {cutoff} ORDER BY id LIMIT 1000)",ct);
                }
                await Task.Delay(TimeSpan.FromHours(1),ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Audit retention failed");await Task.Delay(TimeSpan.FromMinutes(1),ct);}
        }
    }
}

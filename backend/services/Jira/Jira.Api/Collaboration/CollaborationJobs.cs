using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Jira.Api.Collaboration;
public sealed class CollaborationJobs(IServiceScopeFactory scopes,IConfiguration config,ILogger<CollaborationJobs> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                if(config.GetValue("CollaborationJobs:Enabled",false))
                {
                    await using var scope=scopes.CreateAsyncScope();await RunOnce(scope.ServiceProvider.GetRequiredService<JiraDbContext>(),ct);
                }
                await Task.Delay(TimeSpan.FromMinutes(15),ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Collaboration maintenance failed");await Task.Delay(TimeSpan.FromSeconds(30),ct);}
        }
    }
    public static async Task RunOnce(JiraDbContext db,CancellationToken ct)
    {
        
        
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO notifications (id,event_id,tenant_id,recipient_user_id,actor_user_id,issue_id,kind,created_at)
            SELECT gen_random_uuid(),md5(i.tenant_id::text||':'||i.id::text||':'||k.kind||':'||CURRENT_DATE::text)::uuid,
                i.tenant_id,r.user_id,0,i.id,k.kind,CURRENT_TIMESTAMP
            FROM issues i
            CROSS JOIN LATERAL (SELECT i.assignee_user_id AS user_id) r
            CROSS JOIN LATERAL (SELECT CASE WHEN i.due_date<CURRENT_DATE THEN 'ISSUE_OVERDUE' ELSE 'ISSUE_DUE_SOON' END AS kind) k
            WHERE NOT i.is_deleted AND i.status_code<>'DONE' AND i.resolved_at IS NULL AND r.user_id IS NOT NULL
                AND i.due_date<=CURRENT_DATE+1
            ON CONFLICT (tenant_id,event_id,recipient_user_id) DO NOTHING
            """,ct);
        await db.RequestReplays.Where(r=>r.CreatedAt<DateTimeOffset.UtcNow.AddDays(-7)).ExecuteDeleteAsync(ct);
    }
}

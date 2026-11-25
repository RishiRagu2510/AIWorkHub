using Kafka;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Jira.Infrastructure.Persistence.Collaboration;
public sealed class ActivityProjection(JiraDbContext db)
{
    public async Task ApplyAsync(IssueAuditEvent e,CancellationToken ct)
    {
        
        if(e.EntityType.StartsWith("EMPLOYEE",StringComparison.Ordinal))return;
        var entityType=e.EntityType;var entityId=e.EntityId;
        if(entityType=="WORKLOG") {
            using var snapshot=System.Text.Json.JsonDocument.Parse(e.NewValues??e.OldValues??"{}");
            var root=snapshot.RootElement;
            if(root.TryGetProperty("IssueId",out var id)||root.TryGetProperty("issueId",out id)) { entityType="ISSUE";entityId=id.GetInt64(); }
        }
        long? project=entityType=="PROJECT"?entityId:entityType!="ISSUE"?null:await db.Issues.Where(i=>i.TenantId==e.TenantId && i.Id==entityId).Select(i=>(long?)i.ProjectId).SingleOrDefaultAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO issue_activity (event_id,tenant_id,entity_type,entity_id,project_id,actor_user_id,action,old_values,new_values,occurred_at)
            VALUES ({e.EventId},{e.TenantId},{entityType},{entityId},{project},{e.PerformedByUserId},{e.EventType},{e.OldValues},{e.NewValues},{e.OccurredAt})
            ON CONFLICT (event_id) DO NOTHING
            """,ct);
    }
}
public sealed class ActivityConsumer(IOptions<KafkaOptions> options,IServiceScopeFactory scopes,ILogger<ActivityConsumer> logger):ReliableAuditConsumer(options,logger)
{
    protected override string Group=>"aiworkhub-jira-realtime";
    protected override async Task ProcessAsync(IssueAuditEvent message,CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ActivityProjection>().ApplyAsync(message,ct);
    }
}

using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Jira.Application.Abstractions.Persistence;

namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly JiraDbContext _dbContext;

    public OutboxWriter(JiraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Guid eventId,
        string eventType,
        string topic,
        string payload,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage
        {
            EventId = eventId,
            EventType = eventType,
            Topic = topic,
            Payload = payload,
            OccurredAt = occurredAt,
            AttemptCount = 0
        };

        var domainEvent=System.Text.Json.JsonSerializer.Deserialize<Messaging.Events.IssueAuditEvent>(payload);
        if(domainEvent?.EntityType=="ISSUE")
        {
            
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications (id,event_id,tenant_id,recipient_user_id,actor_user_id,issue_id,kind,created_at)
                SELECT gen_random_uuid(),{eventId},{domainEvent.TenantId},w.user_id,{domainEvent.PerformedByUserId},{domainEvent.EntityId},{eventType},{occurredAt}
                FROM issue_watchers w WHERE w.tenant_id={domainEvent.TenantId} AND w.issue_id={domainEvent.EntityId} AND w.user_id<>{domainEvent.PerformedByUserId}
                ON CONFLICT (tenant_id,event_id,recipient_user_id) DO NOTHING
                """,cancellationToken);
            if(eventType is "ISSUE_TRANSITIONED" || eventType=="ISSUE_UPDATED" && domainEvent.OldValues!=domainEvent.NewValues)
            {
                using var oldJson=System.Text.Json.JsonDocument.Parse(domainEvent.OldValues??"{}");
                using var newJson=System.Text.Json.JsonDocument.Parse(domainEvent.NewValues??"{}");
                if(oldJson.RootElement.TryGetProperty("statusCode",out var oldStatus)&&newJson.RootElement.TryGetProperty("statusCode",out var newStatus)&&oldStatus.GetString()!=newStatus.GetString())
                    await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO notifications (id,event_id,tenant_id,recipient_user_id,actor_user_id,issue_id,kind,created_at)
                        SELECT gen_random_uuid(),{eventId},i.tenant_id,r.user_id,{domainEvent.PerformedByUserId},i.id,'ISSUE_TRANSITIONED',{occurredAt}
                        FROM issues i CROSS JOIN LATERAL (SELECT i.assignee_user_id AS user_id UNION SELECT i.reporter_user_id) r
                        WHERE i.tenant_id={domainEvent.TenantId} AND i.id={domainEvent.EntityId} AND r.user_id IS NOT NULL AND r.user_id<>{domainEvent.PerformedByUserId}
                        ON CONFLICT (tenant_id,event_id,recipient_user_id) DO NOTHING
                        """,cancellationToken);
            }
        }

        await _dbContext.OutboxMessages.AddAsync(
            message,
            cancellationToken);
    }
}
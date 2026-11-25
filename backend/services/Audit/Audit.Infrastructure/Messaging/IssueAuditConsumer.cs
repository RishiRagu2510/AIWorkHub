using Audit.Application.Services;
using Kafka;
using Messaging.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Audit.Infrastructure.Messaging;
public sealed class IssueAuditConsumer(IOptions<KafkaOptions> options, IServiceScopeFactory scopes, ILogger<IssueAuditConsumer> logger)
    : ReliableAuditConsumer(options,logger)
{
    protected override string Group => "aiworkhub-audit-service";
    protected override async Task ProcessAsync(IssueAuditEvent e,CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AuditLogService>().RecordAsync(e.EventId,e.EventType,e.TenantId,
            e.PerformedByUserId,e.EntityType,e.EntityId,e.EntityKey,e.EntityType.StartsWith("EMPLOYEE",StringComparison.Ordinal)?"IDENTITY_TENANT":"JIRA",e.OldValues,e.NewValues,e.Metadata,e.OccurredAt,ct);
    }
}

using System.Text.Json;
using Confluent.Kafka;
using Messaging.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Kafka;

public abstract class ReliableAuditConsumer(IOptions<KafkaOptions> options, ILogger logger) : BackgroundService
{
    protected abstract string Group { get; }
    protected abstract Task ProcessAsync(IssueAuditEvent message, CancellationToken ct);
    public static IssueAuditEvent Parse(string payload)
    {
        var e = JsonSerializer.Deserialize<IssueAuditEvent>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (e is null || e.EventId == Guid.Empty || e.TenantId <= 0 || e.EntityId <= 0 || e.PerformedByUserId <= 0 ||
            e.EntityType is not ("ISSUE" or "PROJECT" or "EMPLOYEE" or "EMPLOYEE_REFERENCE" or "WORKLOG" or "TIMESHEET" or "SPRINT" or "COMPONENT" or "RELEASE") || string.IsNullOrWhiteSpace(e.EventType) || string.IsNullOrWhiteSpace(e.EntityKey))
            throw new JsonException("Invalid domain event envelope.");
        return e;
    }
    public static TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(Math.Min(16, Math.Pow(2, Math.Clamp(attempt - 1, 0, 4))));
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        await Task.Yield();
        using var consumer = new ConsumerBuilder<Ignore,string>(new ConsumerConfig {
            BootstrapServers=options.Value.BootstrapServers, GroupId=Group, EnableAutoCommit=false,
            EnableAutoOffsetStore=false, AutoOffsetReset=AutoOffsetReset.Earliest, MaxPollIntervalMs=600000 }).Build();
        using var producer = new ProducerBuilder<string,string>(new ProducerConfig {
            BootstrapServers=options.Value.BootstrapServers, EnableIdempotence=true, MessageTimeoutMs=5000 }).Build();
        consumer.Subscribe("aiworkhub.issue.audit");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore,string>? record;
                try { record=consumer.Consume(stoppingToken); }
                catch(ConsumeException ex) { logger.LogWarning(ex,"Kafka consume failed for {Group}",Group); await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken); continue; }
                if(record is null) continue;
                Exception? failure=null; IssueAuditEvent? message=null; var attempts=0;
                for(var attempt=1;attempt<=5;attempt++)
                {
                    attempts=attempt;
                    try { message=Parse(record.Message.Value ?? "null"); await ProcessAsync(message,stoppingToken); failure=null; break; }
                    catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { throw; }
                    catch(Exception ex) { failure=ex; if(ex is JsonException) break; if(attempt<5) await Task.Delay(RetryDelay(attempt),stoppingToken); }
                }
                if(failure is not null)
                {
                    var dlq=new { event_id=message?.EventId, topic=record.Topic, partition=record.Partition.Value,
                        offset=record.Offset.Value, error=failure.GetType().Name, attempt_count=attempts, failed_at=DateTimeOffset.UtcNow,
                        consumer_group=Group, payload=record.Message.Value };
                    
                    while(true)
                    {
                        try { await producer.ProduceAsync(record.Topic+".dlq",new Message<string,string> {
                            Key=$"{Group}:{record.Topic}:{record.Partition.Value}:{record.Offset.Value}",Value=JsonSerializer.Serialize(dlq)},stoppingToken); break; }
                        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { throw; }
                        catch(Exception ex) { logger.LogWarning(ex,"DLQ unavailable; retaining offset"); await Task.Delay(TimeSpan.FromSeconds(5),stoppingToken); }
                    }
                }
                try { consumer.Commit(record); }
                catch(KafkaException ex) { logger.LogWarning(ex,"Offset commit failed; duplicate-safe replay may follow"); }
            }
        }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
        finally { consumer.Close(); }
    }
}

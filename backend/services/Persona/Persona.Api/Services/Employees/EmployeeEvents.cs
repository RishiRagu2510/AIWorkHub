using System.Text.Json;
using Persona.Domain.Employees;
using Persona.Infrastructure.Persistence;
using Kafka;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class EmployeeEvents(PersonaDbContext db, ITenantContext actor)
{
    public async Task Write(Employee employee, string action, object? before, object? after, CancellationToken ct,
        string entityType = "EMPLOYEE")
    {
        var e = new IssueAuditEvent
        {
            EventId = Guid.NewGuid(), EventType = action, TenantId = actor.TenantId,
            EntityType = entityType, EntityId = employee.Id, EntityKey = employee.EmployeeCode,
            PerformedByUserId = actor.UserId,
            OccurredAt = DateTimeOffset.UtcNow, OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            Metadata = "{\"source\":\"IDENTITY_TENANT\"}"
        };
        var json = JsonSerializer.Serialize(e);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO employee_outbox(event_id,payload,occurred_at) VALUES({e.EventId},{json}::jsonb,{e.OccurredAt})",
            ct);
    }

    public static object Snapshot(Employee e)
    {
        return new
        {
            e.Id, e.UserId, e.EmployeeCode, e.FirstName, e.LastName, e.PreferredName, e.DepartmentId, e.DisciplineId,
            e.JobTitleId, e.DesignationId, e.GradeId, e.CareerProfileId, e.CareerLevelId, e.LocationId,
            e.ManagerEmployeeId,
            e.EmploymentType, e.EmploymentStatus, e.JoiningDate, e.EndDate, e.IsDeleted
        };
    }
}

public sealed class EmployeeOutboxPublisher(IServiceScopeFactory scopes, ILogger<EmployeeOutboxPublisher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        while (!ct.IsCancellationRequested)
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PersonaDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var items = await db.Database.SqlQueryRaw<Pending>(
                        "SELECT id AS \"Id\",payload::text AS \"Payload\",attempt_count AS \"AttemptCount\" FROM employee_outbox WHERE processed_at IS NULL AND (next_attempt_at IS NULL OR next_attempt_at<=NOW()) ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED")
                    .ToListAsync(ct);
                if (items.Count > 0)
                {
                    var item = items[0];
                    try
                    {
                        var e = JsonSerializer.Deserialize<IssueAuditEvent>(item.Payload)!;
                        await scope.ServiceProvider.GetRequiredService<IKafkaProducer>()
                            .PublishAsync("aiworkhub.issue.audit", e, ct);
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE employee_outbox SET processed_at=NOW(),attempt_count=attempt_count+1 WHERE id={item.Id}",
                            ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Employee event {Id} publish failed ({Error}); retrying.", item.Id,
                            ex.GetType().Name);
                        var next = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60,
                            Math.Pow(2, Math.Min(item.AttemptCount, 6))));
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE employee_outbox SET attempt_count=attempt_count+1,next_attempt_at={next} WHERE id={item.Id}",
                            ct);
                    }
                }

                await tx.CommitAsync(ct);
                if (items.Count == 0) await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Employee outbox unavailable ({Error}).", ex.GetType().Name);
                await Task.Delay(3000, ct);
            }
    }

    public sealed class Pending
    {
        public long Id { get; set; }
        public string Payload { get; set; } = "";
        public int AttemptCount { get; set; }
    }
}
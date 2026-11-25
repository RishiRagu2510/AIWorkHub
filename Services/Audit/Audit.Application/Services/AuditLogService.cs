using Audit.Application.Abstractions;
using Audit.Contracts;
using Audit.Domain.Entities;

namespace Audit.Application.Services;

public sealed class AuditLogService
{
    private readonly IAuditLogRepository _repository;

    public AuditLogService(
        IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> RecordAsync(
        Guid eventId,
        string action,
        long tenantId,
        long actorUserId,
        string entityType,
        long entityId,
        string entityKey,
        string source,
        string? oldValues,
        string? newValues,
        string? metadata,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog(
            eventId: eventId,
            tenantId: tenantId,
            actorUserId: actorUserId,
            action: action,
            entityType: entityType,
            entityId: entityId,
            source: source,
            oldValues: oldValues,
            newValues: newValues,
            metadata: metadata ?? "{}",
            occurredAt: occurredAt);

        return await _repository.TryAddAsync(
            auditLog,
            cancellationToken);
    }

    public Task<AuditLogPageDto> SearchAsync(
        long tenantId,
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        return _repository.SearchAsync(
            tenantId,
            query,
            cancellationToken);
    }
}
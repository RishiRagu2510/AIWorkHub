using Audit.Contracts;
using Audit.Domain.Entities;

namespace Audit.Application.Abstractions;

public interface IAuditLogRepository
{
    Task AddAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken = default);

    Task<AuditLogPageDto> SearchAsync(
        long tenantId,
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
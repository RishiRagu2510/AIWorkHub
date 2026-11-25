using Audit.Application.Abstractions;
using Audit.Contracts;
using Audit.Domain.Entities;
using Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Audit.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AuditDbContext _dbContext;

    public AuditLogRepository(
        AuditDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<bool> TryAddAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.AuditLogs.AddAsync(
                auditLog,
                cancellationToken);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return true;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgresException &&
                  postgresException.SqlState ==
                  PostgresErrorCodes.UniqueViolation &&
                  string.Equals(
                      postgresException.ConstraintName,
                      "ux_audit_logs_event_id",
                      StringComparison.Ordinal))
        {
            
            _dbContext.Entry(auditLog).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<AuditLogPageDto> SearchAsync(
        long tenantId,
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        var page =
            Math.Max(query.Page, 1);

        var pageSize =
            Math.Clamp(query.PageSize, 1, 100);

        IQueryable<AuditLog> auditQuery =
            _dbContext.AuditLogs
                .AsNoTracking()
                .Where(log =>
                    log.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType =
                query.EntityType.Trim();

            auditQuery = auditQuery.Where(log =>
                log.EntityType == entityType);
        }

        if (query.EntityId.HasValue)
        {
            auditQuery = auditQuery.Where(log =>
                log.EntityId == query.EntityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action =
                query.Action.Trim();

            auditQuery = auditQuery.Where(log =>
                log.Action == action);
        }

        if (query.ActorUserId.HasValue)
        {
            auditQuery = auditQuery.Where(log =>
                log.ActorUserId == query.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            var source =
                query.Source.Trim();

            auditQuery = auditQuery.Where(log =>
                log.Source == source);
        }

        if (query.From.HasValue)
        {
            auditQuery = auditQuery.Where(log =>
                log.OccurredAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            auditQuery = auditQuery.Where(log =>
                log.OccurredAt <= query.To.Value);
        }

        var totalCount =
            await auditQuery.LongCountAsync(
                cancellationToken);

        var totalPages =
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount / (double)pageSize);

        var items =
            await auditQuery
                .OrderByDescending(log => log.OccurredAt)
                .ThenByDescending(log => log.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(log => new AuditLogDto
                {
                    Id = log.Id,
                    EventId = log.EventId,
                    TenantId = log.TenantId,
                    ActorUserId = log.ActorUserId,
                    Action = log.Action,
                    EntityType = log.EntityType,
                    EntityId = log.EntityId,
                    Source = log.Source,
                    OldValues = log.OldValues,
                    NewValues = log.NewValues,
                    Metadata = log.Metadata,
                    OccurredAt = log.OccurredAt
                })
                .ToListAsync(cancellationToken);

        return new AuditLogPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}
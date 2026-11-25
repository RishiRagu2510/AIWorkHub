using Jira.Application.Services;
using Jira.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Jira.Application.Abstractions.Persistence;
using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly JiraDbContext _dbContext;
    private readonly JiraResourceScope _scope;
    private readonly WritePrecondition _precondition;
    private readonly ProjectAuditWriter _audit;

    public ProjectRepository(JiraDbContext dbContext, JiraResourceScope scope, WritePrecondition precondition, ProjectAuditWriter audit)
    {
        _dbContext = dbContext;
        _scope = scope;
        _precondition = precondition;
        _audit = audit;
    }

    
    
    private Task LockTree(long tenantId, CancellationToken ct)
        => _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenantId})", ct);

    private async Task<Project> RequireProject(long tenantId, long projectId, CancellationToken ct)
        => await _scope.Projects(tenantId).SingleOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new ResourceNotFoundException();

    private async Task<Project> RequireActiveParent(long tenantId, long projectId, CancellationToken ct)
    {
        var project = await RequireProject(tenantId, projectId, ct);
        if (project.Status != ProjectStatus.Active)
            throw new ProjectConflictException("The parent project must be active.");
        return project;
    }

    
    private async Task<Account> ResolveAccount(long tenantId, long? accountId, string name, CancellationToken ct)
    {
        if (accountId.HasValue)
            return await _dbContext.Accounts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId && a.Status == "ACTIVE", ct)
                ?? throw new ResourceNotFoundException();
        var normalized = name.Trim().ToUpperInvariant();
        var account = await _dbContext.Accounts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Name.ToUpper() == normalized, ct);
        if (account is not null) { if (account.Status != "ACTIVE") throw new ProjectConflictException("The account is archived."); return account; }
        account = new Account(tenantId, name);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(ct);
        return account;
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTree(project.TenantId, cancellationToken);
        if (project.ParentProjectId.HasValue && !project.AccountId.HasValue) {
            var parent = await RequireActiveParent(project.TenantId, project.ParentProjectId.Value, cancellationToken);
            project.SelectAccount(parent.AccountId);
        }
        var account = await ResolveAccount(project.TenantId, project.AccountId, project.Name, cancellationToken);
        if (project.ParentProjectId.HasValue) {
            var parent = await RequireActiveParent(project.TenantId, project.ParentProjectId.Value, cancellationToken);
            if (parent.AccountId != account.Id) throw new ProjectConflictException("A subproject must belong to its parent's account.");
        }
        project.SetAccount(account.Id, account.Name);
        await _dbContext.Projects.AddAsync(project, cancellationToken);
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_projects_tenant_key" })
        { throw new ProjectConflictException(); }
        await _audit.RecordAsync(project, "PROJECT_CREATED", null, ProjectAuditWriter.Snapshot(project), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Project> UpdateAsync(long tenantId, long projectId, string name, string? description,
        ProjectType type, ProjectStatus? status, CancellationToken ct, long? accountId = null, string? displayName = null)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockTree(tenantId, ct);
        var project = await RequireProject(tenantId, projectId, ct);
        _precondition.Check(project.Version);
        var before = ProjectAuditWriter.Snapshot(project);
        var account = await ResolveAccount(tenantId, accountId, name, ct);
        if (project.AccountId != account.Id &&
            (project.ParentProjectId.HasValue || await _dbContext.Projects.AnyAsync(p => p.ParentProjectId == project.Id, ct)))
            throw new ProjectConflictException("Detach subprojects before moving a project to another account.");
        project.Rename(displayName);
        project.Update(account.Name, description, type);
        project.SetAccount(account.Id, account.Name);
        switch (status)
        {
            case ProjectStatus.Active: project.Activate(); break;
            case ProjectStatus.Archived: project.Archive(); break;
            case ProjectStatus.Disabled: project.Disable(); break;
        }
        await _dbContext.SaveChangesAsync(ct);
        await _audit.RecordAsync(project, "PROJECT_UPDATED", before, ProjectAuditWriter.Snapshot(project), ct);
        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return project;
    }

    public async Task<Project> MoveAsync(long tenantId, long projectId, long? parentProjectId, CancellationToken ct)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockTree(tenantId, ct);
        var project = await RequireProject(tenantId, projectId, ct);
        _precondition.Check(project.Version);
        var before = ProjectAuditWriter.Snapshot(project);
        if (parentProjectId.HasValue)
        {
            var ancestor = await RequireActiveParent(tenantId, parentProjectId.Value, ct);
            if (ancestor.AccountId != project.AccountId) throw new ProjectConflictException("A subproject must belong to its parent's account.");
            var visited = new HashSet<long>();
            while (true)
            {
                if (ancestor.Id == project.Id || !visited.Add(ancestor.Id))
                    throw new ProjectConflictException("A project cannot be moved beneath itself or its descendants.");
                if (!ancestor.ParentProjectId.HasValue) break;
                ancestor = await RequireProject(tenantId, ancestor.ParentProjectId.Value, ct);
            }
        }
        project.MoveTo(parentProjectId);
        await _dbContext.SaveChangesAsync(ct);
        await _audit.RecordAsync(project, "PROJECT_MOVED", before, ProjectAuditWriter.Snapshot(project), ct);
        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return project;
    }

    public async Task DeleteAsync(long tenantId, long projectId, CancellationToken ct)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockTree(tenantId, ct);
        var project = await RequireProject(tenantId, projectId, ct);
        _precondition.Check(project.Version);
        var before = ProjectAuditWriter.Snapshot(project);
        
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM projects WHERE tenant_id = {tenantId} AND id = {projectId} FOR UPDATE", ct);
        if (await _dbContext.Projects.AnyAsync(p => p.ParentProjectId == projectId, ct) ||
            await _dbContext.Issues.AnyAsync(i => i.ProjectId == projectId, ct) ||
            await _dbContext.ProjectMembers.AnyAsync(m => m.ProjectId == projectId, ct) ||
            await HasLinkedRecords(tenantId, projectId, ct))
            throw new ProjectConflictException("The project contains subprojects, issues, members or linked records. Remove or relocate them before deleting the project, or archive it instead.");
        _dbContext.Projects.Remove(project);
        await _dbContext.SaveChangesAsync(ct);
        await _audit.RecordAsync(project, "PROJECT_DELETED", before, null, ct);
        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<bool> HasLinkedRecords(long tenantId, long projectId, CancellationToken ct)
    {
        
        
        
        const string sql = """
            SELECT format('SELECT EXISTS (SELECT 1 FROM %I.%I r JOIN projects p ON %s WHERE p.id = @projectId AND p.tenant_id = @tenantId)',
                ns.nspname, tbl.relname,
                string_agg(format('r.%I = p.%I', local_col.attname, parent_col.attname), ' AND ' ORDER BY idx.n))
            FROM pg_constraint fk
            JOIN pg_class tbl ON tbl.oid = fk.conrelid
            JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
            CROSS JOIN LATERAL generate_subscripts(fk.conkey, 1) idx(n)
            JOIN pg_attribute local_col ON local_col.attrelid = fk.conrelid AND local_col.attnum = fk.conkey[idx.n]
            JOIN pg_attribute parent_col ON parent_col.attrelid = fk.confrelid AND parent_col.attnum = fk.confkey[idx.n]
            WHERE fk.contype = 'f' AND fk.confrelid = 'projects'::regclass
            GROUP BY fk.oid, ns.nspname, tbl.relname
            """;
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction)_dbContext.Database.CurrentTransaction!.GetDbTransaction();
        var checks = new List<string>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) checks.Add(reader.GetString(0));
        foreach (var check in checks)
        {
            await using var command = new NpgsqlCommand(check, connection, transaction);
            command.Parameters.AddWithValue("projectId", projectId);
            command.Parameters.AddWithValue("tenantId", tenantId);
            if ((bool)(await command.ExecuteScalarAsync(ct))!) return true;
        }
        return false;
    }

    public async Task<IReadOnlyCollection<Project>> GetChildrenAsync(long tenantId, long parentProjectId, CancellationToken cancellationToken)
        => await _scope.Projects(tenantId).AsNoTracking().Where(p => p.ParentProjectId == parentProjectId)
            .OrderBy(p => p.Key).ToListAsync(cancellationToken);

    public Task<Project?> GetByIdAsync(
        long tenantId,
        long projectId,
        CancellationToken cancellationToken)
    {
        return _scope.Projects(tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                project =>
                    project.TenantId == tenantId &&
                    project.Id == projectId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<Project>> GetAllAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        return await _scope.Projects(tenantId)
            .AsNoTracking()
            .Where(project => project.TenantId == tenantId)
            .OrderBy(project => project.Key)
            .ToListAsync(cancellationToken);
    }
}
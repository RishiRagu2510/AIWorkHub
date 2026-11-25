using Jira.Application.Abstractions.Services;
using Jira.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence;

public sealed record ProjectMemberDto(long UserId, string Role, DateTimeOffset CreatedAt);
public sealed record MembershipSource(long ProjectId, string ProjectName, string Role, bool IsInherited);
public sealed record EffectiveProjectMemberDto(long UserId, IReadOnlyList<MembershipSource> Sources);
public sealed class ProjectMembershipService(JiraDbContext db, JiraResourceScope scope, IDeveloperDirectory directory, ProjectAuditWriter audit) : IAssigneeValidator
{
    private async Task<Jira.Domain.Entities.Project> RequireProject(long tenantId, long projectId, CancellationToken ct)
    {
        return await scope.Projects(tenantId).AsNoTracking().SingleOrDefaultAsync(p => p.Id == projectId, ct) ?? throw new ResourceNotFoundException();
    }

    public async Task<IReadOnlyList<ProjectMemberDto>> ListAsync(long tenantId, long projectId, CancellationToken ct)
    {
        await RequireProject(tenantId, projectId, ct);
        return await db.ProjectMembers.AsNoTracking().Where(m => m.TenantId == tenantId && m.ProjectId == projectId)
            .OrderBy(m => m.UserId).Select(m => new ProjectMemberDto(m.UserId, m.Role, m.CreatedAt)).ToListAsync(ct);
    }

    private IQueryable<Jira.Domain.Entities.Project> Ancestors(long tenantId, long projectId)
        => db.Projects.FromSqlInterpolated($"""
            WITH RECURSIVE ancestors AS (
                SELECT p.*, p.xmin FROM projects p WHERE p.id = {projectId} AND p.tenant_id = {tenantId}
                UNION
                SELECT p.*, p.xmin FROM projects p JOIN ancestors child ON child.parent_project_id = p.id
                    AND child.tenant_id = p.tenant_id WHERE p.tenant_id = {tenantId}
            ) SELECT * FROM ancestors
            """);

    public async Task<IReadOnlyList<EffectiveProjectMemberDto>> ListEffectiveAsync(long tenantId, long projectId, CancellationToken ct)
    {
        await RequireProject(tenantId, projectId, ct);
        var ancestors = await Ancestors(tenantId, projectId).AsNoTracking().ToListAsync(ct);
        var ids = ancestors.Select(p => p.Id).ToArray();
        var members = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.TenantId == tenantId && ids.Contains(m.ProjectId)).ToListAsync(ct);
        var sources = members.Select(m => (m.UserId, Source: new MembershipSource(m.ProjectId,
            ancestors.Single(p => p.Id == m.ProjectId).Name, m.Role, m.ProjectId != projectId)))
            .Concat(ancestors.Where(p => p.ProjectLeadUserId.HasValue).Select(p =>
                (p.ProjectLeadUserId!.Value, new MembershipSource(p.Id, p.Name, "PROJECT_LEAD", p.Id != projectId))));
        return sources.GroupBy(m => m.Item1).OrderBy(g => g.Key)
            .Select(g => new EffectiveProjectMemberDto(g.Key, g.Select(m => m.Item2).Distinct()
                .OrderBy(s => s.IsInherited).ThenBy(s => s.ProjectId).ThenBy(s => s.Role).ToArray())).ToArray();
    }

    public async Task ValidateAsync(long tenantId, long projectId, long userId, CancellationToken ct)
    {
        if (userId <= 0) throw new ArgumentException("A valid assignee user ID is required.");
        var user = await directory.GetAssignmentUserAsync(tenantId, userId, ct);
        if (user is null) throw new ArgumentException("The assignee must be an active user with access to this project.");
        
        
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenantId})", ct);
        var ancestors = Ancestors(tenantId, projectId);
        var accessible = user.IsAdmin || await ancestors.AnyAsync(p => p.ProjectLeadUserId == userId ||
            db.ProjectMembers.Any(m => m.TenantId == tenantId && m.ProjectId == p.Id && m.UserId == userId), ct);
        if (!accessible) throw new ArgumentException("The assignee must be an active user with access to this project.");
    }

    public async Task AddAsync(long tenantId, long projectId, long userId, CancellationToken ct)
    {
        await RequireProject(tenantId, projectId, ct);
        if (userId <= 0) throw new ArgumentException("A valid developer user ID is required.");
        if (!await directory.IsActiveDeveloperAsync(tenantId, userId, ct)) throw new ResourceNotFoundException();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenantId})", ct);
        await RequireProject(tenantId, projectId, ct);
        var project = await RequireProject(tenantId, projectId, ct);
        
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO project_members (tenant_id, project_id, user_id, role, created_at) VALUES ({tenantId}, {projectId}, {userId}, 'DEVELOPER', CURRENT_TIMESTAMP) ON CONFLICT (project_id, user_id) DO NOTHING", ct);
        if (changed > 0)
            await audit.RecordAsync(project, "PROJECT_MEMBER_ADDED", null, new { userId, role = "DEVELOPER" }, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RemoveAsync(long tenantId, long projectId, long userId, CancellationToken ct)
    {
        await RequireProject(tenantId, projectId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenantId})", ct);
        var project = await RequireProject(tenantId, projectId, ct);
        var member = await db.ProjectMembers.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == projectId && m.UserId == userId, ct);
        var changed = await db.ProjectMembers.Where(m => m.TenantId == tenantId && m.ProjectId == projectId && m.UserId == userId).ExecuteDeleteAsync(ct);
        if (changed > 0)
            await audit.RecordAsync(project, "PROJECT_MEMBER_REMOVED", new { userId, role = member!.Role }, null, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

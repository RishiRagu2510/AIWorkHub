using Jira.Contracts.Projects;
using Jira.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Jira.Infrastructure.Persistence.Allocations;

public enum AllocationOutcome { Created, InvalidDates, ProjectNotFound, ProjectNotActive, Overlap, OverCapacity }


public sealed record AllocationResult(AllocationOutcome Outcome, string? Message = null);





public sealed class AllocationService(JiraDbContext db, ITenantContext actor, ProjectMembershipService members, ProjectAuditWriter audit)
{
    private sealed class Row
    {
        public long Id { get; set; }
        public long ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public long UserId { get; set; }
        public int Percentage { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }

    private IQueryable<Row> Rows(long userId) => db.Database.SqlQuery<Row>($"""
        SELECT a.id AS "Id",a.project_id AS "ProjectId",COALESCE(NULLIF(p.display_name,''),p.name) AS "ProjectName",a.user_id AS "UserId",a.percentage AS "Percentage",a.start_date AS "StartDate",a.end_date AS "EndDate"
        FROM project_allocations a JOIN projects p ON p.id=a.project_id AND p.tenant_id=a.tenant_id
        WHERE a.tenant_id={actor.TenantId} AND a.user_id={userId}
        """);

    private Task Lock(CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-actor.TenantId})", ct);

    public async Task<IReadOnlyList<AllocationDto>> List(long userId, CancellationToken ct)
    {
        var rows = await Rows(userId).OrderByDescending(r => r.StartDate).ToArrayAsync(ct);
        return rows.Select(r => new AllocationDto(r.Id, r.ProjectId, r.ProjectName, r.UserId, r.Percentage, r.StartDate, r.EndDate)).ToArray();
    }

    public async Task<AllocationResult> Create(AllocationRequest r, CancellationToken ct)
    {
        if (r.StartDate == default || r.EndDate < r.StartDate) return new(AllocationOutcome.InvalidDates, "Enter a valid allocation date range.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var project = await db.Projects.SingleOrDefaultAsync(p => p.TenantId == actor.TenantId && p.Id == r.ProjectId, ct);
        if (project is null) return new(AllocationOutcome.ProjectNotFound);
        if (project.Status != ProjectStatus.Active) return new(AllocationOutcome.ProjectNotActive);
        await members.ValidateAsync(actor.TenantId, r.ProjectId, r.UserId, ct);
        var existing = await Rows(r.UserId).ToArrayAsync(ct);
        var end = r.EndDate ?? DateOnly.MaxValue;
        var overlap = existing.Where(x => x.StartDate <= end && (x.EndDate ?? DateOnly.MaxValue) >= r.StartDate).ToArray();
        if (overlap.Any(x => x.ProjectId == r.ProjectId)) return new(AllocationOutcome.Overlap, "This project already has an overlapping allocation.");
        var points = overlap.Select(x => x.StartDate < r.StartDate ? r.StartDate : x.StartDate).Append(r.StartDate);
        if (points.Any(d => r.Percentage + overlap.Where(x => x.StartDate <= d && (x.EndDate ?? DateOnly.MaxValue) >= d).Sum(x => x.Percentage) > 100))
            return new(AllocationOutcome.OverCapacity, "Total allocation cannot exceed 100%.");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO project_allocations(tenant_id,project_id,user_id,percentage,start_date,end_date) VALUES({actor.TenantId},{r.ProjectId},{r.UserId},{r.Percentage},{r.StartDate},{r.EndDate})", ct);
        await audit.RecordAsync(project, "ALLOCATION_CREATED", null, r, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(AllocationOutcome.Created);
    }

    
    public async Task<bool> Delete(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var projectId = await db.Database.SqlQuery<long>($"SELECT project_id AS \"Value\" FROM project_allocations WHERE id={id} AND tenant_id={actor.TenantId}").SingleOrDefaultAsync(ct);
        if (projectId == 0) return false;
        var project = await db.Projects.SingleAsync(p => p.Id == projectId && p.TenantId == actor.TenantId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM project_allocations WHERE id={id} AND tenant_id={actor.TenantId}", ct);
        await audit.RecordAsync(project, "ALLOCATION_REMOVED", new { id }, null, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}

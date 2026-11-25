using System.Text.Json;
using Jira.Api.Controllers;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Services;
using Jira.Contracts.Planning;
using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Collaboration;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Jira.Api.Services.Planning;






public sealed class PlanningService(JiraDbContext db, JiraResourceScope scope, ITenantContext actor, IJiraUnitOfWork unit, IOutboxWriter outbox, WritePrecondition version)
{
    private const int PageSize = 50;

    

    private async Task RequireProject(long id, CancellationToken ct)
    {
        if (!await scope.Projects(actor.TenantId).AnyAsync(p => p.Id == id, ct)) throw new ResourceNotFoundException();
    }

    private Task Lock(CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-actor.TenantId})", ct);

    private async Task Audit(long project, string type, long id, string action, object? before, object after, CancellationToken ct)
    {
        var e = new IssueAuditEvent
        {
            EventId = Guid.NewGuid(), EventType = action, TenantId = actor.TenantId, EntityType = type, EntityId = id, EntityKey = $"{type}-{id}",
            PerformedByUserId = actor.UserId, OccurredAt = DateTimeOffset.UtcNow,
            OldValues = before is null ? null : JsonSerializer.Serialize(before), NewValues = JsonSerializer.Serialize(after),
            Metadata = JsonSerializer.Serialize(new { projectId = project })
        };
        await outbox.AddAsync(e.EventId, action, "aiworkhub.issue.audit", JsonSerializer.Serialize(e), e.OccurredAt, ct);
    }

    private async Task<Board> RequireBoard(long id, CancellationToken ct) =>
        await db.Boards.SingleOrDefaultAsync(b => b.Id == id && b.TenantId == actor.TenantId && scope.Projects(actor.TenantId).Any(p => p.Id == b.ProjectId), ct) ?? throw new ResourceNotFoundException();

    private async Task<Sprint> RequireSprint(long id, CancellationToken ct) =>
        await db.Set<Sprint>().SingleOrDefaultAsync(s => s.Id == id && s.TenantId == actor.TenantId && scope.Projects(actor.TenantId).Any(p => p.Id == s.ProjectId), ct) ?? throw new ResourceNotFoundException();

    private static bool KnownStatus(string status) => status is "UNRELEASED" or "RELEASED" or "ARCHIVED";

    private static ComponentDto ToDto(ProjectComponent c) => new(c.Id, c.TenantId, c.ProjectId, c.Name, c.Description, c.CreatedAt, c.UpdatedAt, c.Version);
    private static ReleaseDto ToDto(ProjectRelease r) => new(r.Id, r.TenantId, r.ProjectId, r.Name, r.ReleaseVersion, r.Description, r.Status, r.ReleaseDate, r.CreatedAt, r.UpdatedAt, r.Version);
    private static SprintDto ToDto(Sprint s) => new(s.Id, s.TenantId, s.ProjectId, s.BoardId, s.Name, s.Goal, s.Status, s.StartDate, s.EndDate, s.CreatedAt, s.UpdatedAt, s.Version);
    private static BoardDto ToDto(Board b) => new(b.Id, b.TenantId, b.ProjectId, b.Name, b.BoardType, b.FilterDefinition, b.CreatedAt, b.UpdatedAt);

    

    public async Task<IReadOnlyList<ComponentDto>> Components(long projectId, CancellationToken ct)
    {
        await RequireProject(projectId, ct);
        var items = await db.Set<ProjectComponent>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId).OrderBy(x => x.Name).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<ComponentDto> CreateComponent(long projectId, NamedRequest request, CancellationToken ct)
    {
        await RequireProject(projectId, ct);
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Name is required.");
        var item = new ProjectComponent { TenantId = actor.TenantId, ProjectId = projectId, Name = request.Name.Trim(), Description = request.Description };
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(t);
            if (await db.Set<ProjectComponent>().AnyAsync(x => x.ProjectId == projectId && x.Name == item.Name, t)) throw new ProjectConflictException("Component already exists.");
            db.Add(item);
            await db.SaveChangesAsync(t);
            await Audit(projectId, "COMPONENT", item.Id, "COMPONENT_CREATED", null, item, t);
            await db.SaveChangesAsync(t);
        }, ct);
        return ToDto(item);
    }

    public async Task<ComponentDto> UpdateComponent(long projectId, long id, NamedRequest request, CancellationToken ct)
    {
        await RequireProject(projectId, ct);
        ProjectComponent item = null!;
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(t);
            item = await db.Set<ProjectComponent>().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId, t) ?? throw new ResourceNotFoundException();
            version.Check(item.Version);
            if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Name is required.");
            if (await db.Set<ProjectComponent>().AnyAsync(x => x.ProjectId == projectId && x.Id != id && x.Name == request.Name.Trim(), t)) throw new ProjectConflictException("Component already exists.");
            var before = JsonSerializer.SerializeToElement(item);
            item.Name = request.Name.Trim();
            item.Description = request.Description;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await Audit(projectId, "COMPONENT", id, "COMPONENT_UPDATED", before, item, t);
            await db.SaveChangesAsync(t);
        }, ct);
        return ToDto(item);
    }

    

    public async Task<IReadOnlyList<ReleaseDto>> Versions(long projectId, CancellationToken ct)
    {
        await RequireProject(projectId, ct);
        var items = await db.Set<ProjectRelease>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId).OrderBy(x => x.Name).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<ReleaseDto> CreateVersion(long projectId, VersionRequest request, CancellationToken ct)
    {
        await RequireProject(projectId, ct);
        if (string.IsNullOrWhiteSpace(request.Name) || !KnownStatus(request.Status)) throw new ArgumentException("Invalid version name or status.");
        var item = new ProjectRelease
        {
            TenantId = actor.TenantId, ProjectId = projectId, Name = request.Name.Trim(), ReleaseVersion = request.Name.Trim(),
            Description = request.Description, Status = request.Status, ReleaseDate = request.ReleaseDate
        };
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(t);
            if (await db.Set<ProjectRelease>().AnyAsync(x => x.ProjectId == projectId && x.ReleaseVersion == item.ReleaseVersion, t)) throw new ProjectConflictException("Version already exists.");
            db.Add(item);
            await db.SaveChangesAsync(t);
            await Audit(projectId, "RELEASE", item.Id, "VERSION_CREATED", null, item, t);
            await db.SaveChangesAsync(t);
        }, ct);
        return ToDto(item);
    }

    public async Task<ReleaseDto> UpdateVersion(long projectId, long id, VersionRequest request, CancellationToken ct)
    {
        await RequireProject(projectId, ct);
        ProjectRelease item = null!;
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(t);
            item = await db.Set<ProjectRelease>().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId, t) ?? throw new ResourceNotFoundException();
            version.Check(item.Version);
            if (string.IsNullOrWhiteSpace(request.Name) || !KnownStatus(request.Status)) throw new ArgumentException("Invalid version name or status.");
            if (await db.Set<ProjectRelease>().AnyAsync(x => x.ProjectId == projectId && x.Id != id && x.ReleaseVersion == request.Name.Trim(), t)) throw new ProjectConflictException("Version already exists.");
            var before = JsonSerializer.SerializeToElement(item);
            item.Name = request.Name.Trim();
            item.ReleaseVersion = item.Name;
            item.Description = request.Description;
            item.Status = request.Status;
            item.ReleaseDate = request.ReleaseDate;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await Audit(projectId, "RELEASE", id, "VERSION_UPDATED", before, item, t);
            await db.SaveChangesAsync(t);
        }, ct);
        return ToDto(item);
    }

    

    public async Task<IReadOnlyList<string>> Labels(long projectId, CancellationToken ct)
    {
        await RequireProject(projectId, ct);
        return await scope.Issues(actor.TenantId).Where(i => i.ProjectId == projectId).SelectMany(i => i.Labels).Select(l => l.Label).Distinct().Order().ToListAsync(ct);
    }

    

    public async Task<IReadOnlyList<BoardDto>> Boards(long projectId, CancellationToken ct)
    {
        var boards = await db.Boards.AsNoTracking()
            .Where(b => b.TenantId == actor.TenantId && b.ProjectId == projectId && scope.Projects(actor.TenantId).Any(p => p.Id == b.ProjectId))
            .ToListAsync(ct);
        return boards.Select(ToDto).ToList();
    }

    

    public async Task<IReadOnlyList<SprintDto>> Sprints(long boardId, CancellationToken ct)
    {
        await RequireBoard(boardId, ct);
        var items = await db.Set<Sprint>().AsNoTracking().Where(s => s.TenantId == actor.TenantId && s.BoardId == boardId).OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<SprintDto> CreateSprint(long boardId, SprintRequest request, CancellationToken ct)
    {
        var board = await RequireBoard(boardId, ct);
        if (board.BoardType != "SCRUM") throw new ArgumentException("Sprints require a Scrum board.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndDate < request.StartDate) throw new ArgumentException("Invalid sprint name or dates.");
        var item = new Sprint { TenantId = actor.TenantId, ProjectId = board.ProjectId, BoardId = boardId, Name = request.Name.Trim(), Goal = request.Goal, StartDate = request.StartDate, EndDate = request.EndDate };
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(t);
            db.Add(item);
            await db.SaveChangesAsync(t);
            await Audit(item.ProjectId, "SPRINT", item.Id, "SPRINT_CREATED", null, item, t);
            await db.SaveChangesAsync(t);
        }, ct);
        return ToDto(item);
    }

    public Task<SprintDto> StartSprint(long id, CancellationToken ct) => ChangeSprint(id, complete: false, ct);

    public Task<SprintDto> CompleteSprint(long id, CancellationToken ct) => ChangeSprint(id, complete: true, ct);

    private async Task<SprintDto> ChangeSprint(long id, bool complete, CancellationToken ct)
    {
        Sprint item = null!;
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(t);
            item = await RequireSprint(id, t);
            version.Check(item.Version);
            var before = JsonSerializer.SerializeToElement(item);
            if (item.Status != (complete ? "ACTIVE" : "PLANNED")) throw new ProjectConflictException("Sprint cannot make that transition.");
            if (!complete)
            {
                if (item.StartDate is null || item.EndDate is null || item.EndDate < item.StartDate) throw new ArgumentException("Set valid sprint dates before starting.");
                if (await db.Set<Sprint>().AnyAsync(s => s.TenantId == actor.TenantId && s.BoardId == item.BoardId && s.Status == "ACTIVE", t)) throw new ProjectConflictException("This board already has an active sprint.");
            }
            else
            {
                
                var unfinished = await db.Set<SprintIssue>().Where(si => si.SprintId == id && db.Issues.Any(i => i.Id == si.IssueId && i.StatusCode != "DONE" && i.StatusCode != "CANCELLED")).ToListAsync(t);
                db.RemoveRange(unfinished);
                foreach (var link in unfinished)
                {
                    var issue = await db.Issues.SingleAsync(i => i.Id == link.IssueId && i.TenantId == actor.TenantId, t);
                    issue.Rank(issue.BacklogOrder);
                    await CollaborationAudit.Write(outbox, issue, actor.UserId, "ISSUE_SPRINT_CHANGED", new { sprintId = id }, new { sprintId = (long?)null }, t);
                }
            }
            item.Status = complete ? "COMPLETED" : "ACTIVE";
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await Audit(item.ProjectId, "SPRINT", item.Id, complete ? "SPRINT_COMPLETED" : "SPRINT_STARTED", before, item, t);
            await db.SaveChangesAsync(t);
        }, ct);
        return ToDto(item);
    }

    

    public async Task<BacklogPageDto> Backlog(long boardId, long? sprintId, int page, CancellationToken ct)
    {
        var board = await RequireBoard(boardId, ct);
        if (page < 1 || page > 100000) throw new ArgumentException("Invalid page.");
        if (sprintId.HasValue)
        {
            var s = await RequireSprint(sprintId.Value, ct);
            if (s.BoardId != boardId) throw new ResourceNotFoundException();
        }
        var query = scope.Issues(actor.TenantId).Where(i => i.ProjectId == board.ProjectId);
        if (sprintId.HasValue)
            query = query.Where(i => db.Set<SprintIssue>().Any(si => si.IssueId == i.Id && si.SprintId == sprintId));
        else
            query = query.Where(i => i.StatusCode != "DONE" && i.StatusCode != "CANCELLED" && !db.Set<SprintIssue>().Any(si => si.IssueId == i.Id && db.Set<Sprint>().Any(s => s.Id == si.SprintId && (s.Status == "PLANNED" || s.Status == "ACTIVE"))));
        var items = await query.OrderBy(i => i.BacklogOrder).ThenBy(i => i.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(i => new BacklogItemDto(i.Id, i.IssueKey, i.Summary, i.StatusCode, i.Version, i.BacklogOrder)).ToListAsync(ct);
        return new BacklogPageDto(items, page, PageSize, await query.CountAsync(ct));
    }

    public async Task Place(long boardId, long issueId, Placement request, CancellationToken ct)
    {
        var board = await RequireBoard(boardId, ct);
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(t);
            var issue = await scope.Issues(actor.TenantId).SingleOrDefaultAsync(i => i.Id == issueId && i.ProjectId == board.ProjectId, t) ?? throw new ResourceNotFoundException();
            version.Check(issue.Version);
            if (request.SprintId.HasValue)
            {
                var s = await RequireSprint(request.SprintId.Value, t);
                if (s.BoardId != boardId) throw new ResourceNotFoundException();
                if (s.Status is not ("PLANNED" or "ACTIVE")) throw new ProjectConflictException("Completed sprints are locked.");
            }
            var links = await db.Set<SprintIssue>().Where(si => si.IssueId == issueId && db.Set<Sprint>().Any(s => s.Id == si.SprintId && (s.Status == "PLANNED" || s.Status == "ACTIVE"))).ToListAsync(t);
            var before = new { sprintIds = links.Select(l => l.SprintId).ToArray(), position = issue.BacklogOrder };
            db.RemoveRange(links.Where(l => l.SprintId != request.SprintId));
            if (request.SprintId.HasValue && !links.Any(l => l.SprintId == request.SprintId)) db.Add(new SprintIssue { IssueId = issueId, SprintId = request.SprintId.Value });
            issue.Rank(request.Position);
            await CollaborationAudit.Write(outbox, issue, actor.UserId, "ISSUE_SPRINT_CHANGED", before, new { sprintId = request.SprintId, position = request.Position }, t);
            await db.SaveChangesAsync(t);
        }, ct);
    }
}

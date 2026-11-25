using Jira.Application.Services;
using Jira.Contracts.Collaboration;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Collaboration;
using Microsoft.EntityFrameworkCore;
using TenantContext;
namespace Jira.Api.Collaboration;
public sealed class CollaborationService(JiraDbContext db,JiraResourceScope scope,ITenantContext context)
{
    public async Task RequireIssue(long id,CancellationToken ct)
    {
        if(!context.IsAuthenticated || !await scope.Issues(context.TenantId).AnyAsync(i=>i.Id==id,ct)) throw new ResourceNotFoundException();
    }
    public async Task<Board> RequireBoard(long id,CancellationToken ct)
        => await db.Boards.AsNoTracking().Where(b=>b.TenantId==context.TenantId && scope.Projects(context.TenantId).Any(p=>p.Id==b.ProjectId))
            .SingleOrDefaultAsync(b=>b.Id==id,ct) ?? throw new ResourceNotFoundException();
    public IQueryable<ActivityEvent> Activities() => db.ActivityEvents.AsNoTracking().Where(e=>e.TenantId==context.TenantId);
    private static async Task<ActivityPageDto> ActivityPage(IQueryable<ActivityEvent> query,int page,CancellationToken ct)
    {
        var items=await query.OrderByDescending(e=>e.OccurredAt).ThenBy(e=>e.EventId).Skip((page-1)*50).Take(50).ToListAsync(ct);
        return new ActivityPageDto(items.Select(e=>new ActivityEventDto(e.EventId,e.TenantId,e.EntityType,e.EntityId,e.ProjectId,e.ActorUserId,e.Action,e.OldValues,e.NewValues,e.OccurredAt)).ToList(),page,50,await query.CountAsync(ct));
    }
    public async Task<ActivityPageDto> History(long issue,int page,CancellationToken ct)
    {
        await RequireIssue(issue,ct); if(page<1 || page>100000) throw new ArgumentException("Invalid page.");
        return await ActivityPage(Activities().Where(e=>e.EntityType=="ISSUE" && e.EntityId==issue),page,ct);
    }
    
    public async Task<ActivityPageDto?> Feed(int page,CancellationToken ct)
        => page<1 || page>100000 ? null : await ActivityPage(Activities(),page,ct);
    public async Task Watch(long issue,bool enabled,CancellationToken ct)
    {
        await RequireIssue(issue,ct);
        if(enabled) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO issue_watchers (tenant_id,issue_id,user_id) VALUES ({context.TenantId},{issue},{context.UserId}) ON CONFLICT DO NOTHING",ct);
        else await db.Watchers.Where(w=>w.TenantId==context.TenantId && w.IssueId==issue && w.UserId==context.UserId).ExecuteDeleteAsync(ct);
    }
    public async Task<object> Watchers(long issue,CancellationToken ct)
    {
        await RequireIssue(issue,ct);
        return await db.Watchers.Where(w=>w.TenantId==context.TenantId && w.IssueId==issue).OrderBy(w=>w.UserId).Select(w=>w.UserId).ToListAsync(ct);
    }
    public async Task<object> BoardView(long id,int page,CancellationToken ct)
    {
        var board=await RequireBoard(id,ct);if(page<1 || page>100000)throw new ArgumentException("Invalid page.");
        var issues=scope.Issues(context.TenantId).Where(i=>i.ProjectId==board.ProjectId);
        return new {board,columns=await db.BoardColumns.AsNoTracking().Where(c=>c.BoardId==id).OrderBy(c=>c.Position).ToListAsync(ct),
            items=await issues.OrderBy(i=>i.IssueNumber).Skip((page-1)*100).Take(100)
                .Select(i=>new{i.Id,i.IssueKey,i.Summary,i.StatusCode,i.AssigneeUserId,AssigneeUserIds=i.Assignees.Select(a=>a.UserId).ToArray(),i.Priority,i.Version}).ToListAsync(ct),page,pageSize=100,totalCount=await issues.CountAsync(ct)};
    }
    public async Task<long> CreateBoard(long project,string name,string type,WorkflowDefinition definition,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>200 || type is not("KANBAN" or "SCRUM"))throw new ArgumentException("Invalid board name/type.");
        if(!await scope.Projects(context.TenantId).AnyAsync(p=>p.Id==project,ct))throw new ResourceNotFoundException();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var board=new Board{TenantId=context.TenantId,ProjectId=project,Name=name.Trim(),BoardType=type};db.Boards.Add(board);await db.SaveChangesAsync(ct);
        db.BoardColumns.AddRange(definition.Statuses.Select((s,n)=>new BoardColumn{BoardId=board.Id,Name=s,StatusCode=s,Position=n}));
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return board.Id;
    }
}

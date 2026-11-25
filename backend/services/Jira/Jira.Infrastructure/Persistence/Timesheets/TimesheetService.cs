using System.Text.Json;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Abstractions.Services;
using Jira.Application.Services;
using Jira.Contracts.Timesheets;
using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence.Notifications;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using TenantContext;
namespace Jira.Infrastructure.Persistence.Timesheets;

public sealed class TimesheetService(JiraDbContext db,JiraResourceScope scope,ITenantContext actor,
    IJiraUnitOfWork unit,IOutboxWriter outbox,WritePrecondition precondition,ITimesheetApproverDirectory directory)
{
    public static DateOnly Monday(DateOnly date)=>date.AddDays(-(((int)date.DayOfWeek+6)%7));
    private void Auth(){if(!actor.IsAuthenticated||actor.UserId<=0||actor.TenantId<=0)throw new ResourceNotFoundException();}
    private Task Lock(CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-actor.TenantId})",ct);
    public IQueryable<WorkLog> Visible(bool team=false) {
        var issues=scope.Issues(actor.TenantId);
        return db.WorkLogs.Where(w=>actor.IsAuthenticated&&w.TenantId==actor.TenantId&&(team||w.UserId==actor.UserId)&&issues.Any(i=>i.Id==w.IssueId&&i.ProjectId==w.ProjectId));
    }
    public IQueryable<TimesheetPeriod> VisiblePeriods(bool team=false) {
        var issues=scope.Issues(actor.TenantId);
        return db.TimesheetPeriods.Where(p=>actor.IsAuthenticated&&p.TenantId==actor.TenantId&&
            (p.UserId==actor.UserId || team && db.WorkLogs.Any(w=>w.PeriodId==p.Id)&&
             !db.WorkLogs.Any(w=>w.PeriodId==p.Id&&!issues.Any(i=>i.Id==w.IssueId))));
    }
    private IQueryable<WorkLogDto> Dtos(IQueryable<WorkLog> query)=>from w in query
        join i in db.Issues on w.IssueId equals i.Id join p in db.Projects on w.ProjectId equals p.Id
        join period in db.TimesheetPeriods on w.PeriodId equals period.Id
        select new WorkLogDto(w.Id,w.UserId,w.IssueId,w.ProjectId,w.PeriodId,w.WorkDate,w.DurationMinutes,w.Description,w.Billable,
            w.CreatedAt,w.UpdatedAt,w.Version,i.IssueKey,i.Summary,p.DisplayName,period.Status,w.StartTime,w.EndTime);
    public async Task<WorkLogDto> Get(long id,bool team,CancellationToken ct)=>
        await Dtos(Visible(team).Where(w=>w.Id==id)).SingleOrDefaultAsync(ct)??throw new ResourceNotFoundException();
    public static (DateOnly From,DateOnly To) Range(TimesheetQuery q) {
        var from=q.From??Monday(DateOnly.FromDateTime(DateTime.UtcNow));var to=q.To??from.AddDays(6);
        if(from.Year<2000||to<from||to.DayNumber-from.DayNumber>365||q.Page<1||q.Page>100000||q.PageSize<1||q.PageSize>100||q.UserId<=0||q.IssueId<=0||q.ProjectId<=0)
            throw new ArgumentException("Use a valid date range of at most 366 days, positive IDs and pageSize 1–100.");
        if(q.Status is not null && q.Status is not ("DRAFT" or "SUBMITTED" or "APPROVED" or "REJECTED"))throw new ArgumentException("Invalid period status.");
        return(from,to);
    }
    private IQueryable<WorkLog> Filter(TimesheetQuery q,bool team) {
        var (from,to)=Range(q);var rows=Visible(team).Where(w=>w.WorkDate>=from&&w.WorkDate<=to);
        if(q.UserId.HasValue)rows=rows.Where(w=>w.UserId==q.UserId);
        if(q.ProjectId.HasValue)rows=rows.Where(w=>w.ProjectId==q.ProjectId);
        if(q.IssueId.HasValue)rows=rows.Where(w=>w.IssueId==q.IssueId);
        if(q.Billable.HasValue)rows=rows.Where(w=>w.Billable==q.Billable);
        if(q.Status!=null)rows=rows.Where(w=>db.TimesheetPeriods.Any(p=>p.Id==w.PeriodId&&p.Status==q.Status));
        return rows;
    }
    public async Task<WorkLogPage> List(TimesheetQuery q,bool team,CancellationToken ct) {
        Auth();var query=Filter(q,team);var count=await query.CountAsync(ct);
        var items=await Dtos(query.OrderByDescending(w=>w.WorkDate).ThenByDescending(w=>w.Id).Skip((q.Page-1)*q.PageSize).Take(q.PageSize)).ToListAsync(ct);
        return new(items,q.Page,q.PageSize,count,(int)Math.Ceiling(count/(double)q.PageSize));
    }
    public async Task<TimesheetReport> Report(TimesheetQuery q,bool team,CancellationToken ct) {
        Auth();var(from,to)=Range(q);var rows=Filter(q,team);
        var total=await rows.GroupBy(w=>1).Select(g=>new TimeTotal(g.Sum(w=>(long)w.DurationMinutes),g.Sum(w=>w.Billable?(long)w.DurationMinutes:0),g.Sum(w=>!w.Billable?(long)w.DurationMinutes:0))).SingleOrDefaultAsync(ct)??new(0,0,0);
        var days=await rows.GroupBy(w=>w.WorkDate).OrderBy(g=>g.Key).Select(g=>new DayTotal(g.Key,g.Sum(w=>(long)w.DurationMinutes),g.Sum(w=>w.Billable?(long)w.DurationMinutes:0))).ToListAsync(ct);
        var users=await rows.GroupBy(w=>w.UserId).OrderBy(g=>g.Key).Select(g=>new GroupTotal(g.Key,g.Sum(w=>(long)w.DurationMinutes),g.Sum(w=>w.Billable?(long)w.DurationMinutes:0))).ToListAsync(ct);
        var projects=await rows.GroupBy(w=>w.ProjectId).OrderBy(g=>g.Key).Select(g=>new GroupTotal(g.Key,g.Sum(w=>(long)w.DurationMinutes),g.Sum(w=>w.Billable?(long)w.DurationMinutes:0))).ToListAsync(ct);
        var issues=await rows.GroupBy(w=>w.IssueId).OrderBy(g=>g.Key).Select(g=>new GroupTotal(g.Key,g.Sum(w=>(long)w.DurationMinutes),g.Sum(w=>w.Billable?(long)w.DurationMinutes:0))).ToListAsync(ct);
        var periods=VisiblePeriods(team).Where(p=>p.PeriodStart<=to&&p.PeriodEnd>=from);
        if(q.UserId.HasValue)periods=periods.Where(p=>p.UserId==q.UserId);
        if(q.Status!=null)periods=periods.Where(p=>p.Status==q.Status);
        if(q.ProjectId.HasValue||q.IssueId.HasValue||q.Billable.HasValue)periods=periods.Where(p=>rows.Any(w=>w.PeriodId==p.Id));
        var count=await periods.CountAsync(ct);
        var visible=Visible(team);
        var periodItems=await periods.OrderByDescending(p=>p.PeriodStart).ThenBy(p=>p.Id).Skip((q.Page-1)*q.PageSize).Take(q.PageSize)
          .Select(p=>new PeriodDto(p.Id,p.UserId,p.PeriodStart,p.PeriodEnd,p.Status,p.SubmittedAt,p.ApproverUserId,p.ApprovedByUserId,p.ApprovedAt,p.RejectionReason,p.Version,
              visible.Where(w=>w.PeriodId==p.Id).Sum(w=>(long)w.DurationMinutes),
              visible.Where(w=>w.PeriodId==p.Id&&w.Billable).Sum(w=>(long)w.DurationMinutes),null)).ToListAsync(ct);
        var ids=periodItems.Select(p=>p.Id).ToArray();
        var projectTotals=await PeriodProjects(ids,team,ct);
        periodItems=periodItems.Select(p=>p with {Projects=projectTotals.Where(t=>t.PeriodId==p.Id).Select(t=>t.Project).ToArray()}).ToList();
        return new(from,to,total.TotalMinutes,total.BillableMinutes,total.NonBillableMinutes,days,users,projects,issues,await List(q,team,ct),periodItems,count);
    }
    public async Task<TimesheetHierarchyPage> Hierarchy(TimesheetQuery query, bool team, CancellationToken ct)
    {
        Auth();
        var rows = Filter(query, team);
        var groups = rows.GroupBy(w => new { w.UserId, w.ProjectId }).Select(g => new {
            g.Key.UserId, g.Key.ProjectId, Minutes = g.Sum(w => (long)w.DurationMinutes),
            BillableMinutes = g.Sum(w => w.Billable ? (long)w.DurationMinutes : 0)
        });
        var count = await groups.CountAsync(ct);
        var page = groups.OrderBy(g => g.UserId).ThenBy(g => g.ProjectId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
        var selected = await (from g in page join p in db.Projects on g.ProjectId equals p.Id
            select new { g.UserId, g.ProjectId, p.DisplayName, g.Minutes, g.BillableMinutes }).ToListAsync(ct);
        var times = await rows.Where(w => page.Any(g => g.UserId == w.UserId && g.ProjectId == w.ProjectId))
            .GroupBy(w => new { w.UserId, w.ProjectId, w.IssueId })
            .Select(g => new { g.Key.UserId, g.Key.ProjectId, g.Key.IssueId,
                Minutes = g.Sum(w => (long)w.DurationMinutes), BillableMinutes = g.Sum(w => w.Billable ? (long)w.DurationMinutes : 0) }).ToListAsync(ct);
        var ids = times.Select(w => w.IssueId).Distinct().ToArray();
        var accessible = scope.Issues(actor.TenantId).AsNoTracking();
        var issues = await accessible.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        
        for (var depth = 0; depth < 4; depth++)
        {
            var parents = issues.Values.Where(i => i.ParentIssueId.HasValue && !issues.ContainsKey(i.ParentIssueId.Value))
                .Select(i => i.ParentIssueId!.Value).Distinct().ToArray();
            if (parents.Length == 0) break;
            var found = await accessible.Where(i => parents.Contains(i.Id)).ToListAsync(ct);
            if (found.Count == 0) break;
            foreach (var parent in found) issues[parent.Id] = parent;
        }
        var result = selected.Select(g => {
            var direct = times.Where(w => w.UserId == g.UserId && w.ProjectId == g.ProjectId).ToDictionary(w => w.IssueId);
            var included = new HashSet<long>(direct.Keys);
            foreach (var id in direct.Keys)
            {
                var current = id;
                var visited = new HashSet<long>();
                while (visited.Add(current) && issues.TryGetValue(current, out var item) && item.ParentIssueId is long parent &&
                       issues.TryGetValue(parent, out var ancestor) && ancestor.ProjectId == g.ProjectId)
                { included.Add(parent); current = parent; }
            }
            var nodes = included.Where(issues.ContainsKey).Order().Select(id => {
                var issue = issues[id]; direct.TryGetValue(id, out var time);
                return new TimesheetHierarchyNode(id, issue.IssueKey, issue.Summary,
                    issue.IssueType.ToString().ToUpperInvariant(),
                    issue.ParentIssueId is long parent && included.Contains(parent) ? parent : null,
                    time?.Minutes ?? 0, time?.BillableMinutes ?? 0);
            }).ToArray();
            return new TimesheetProjectGroup(g.UserId, g.ProjectId, g.DisplayName, g.Minutes, g.BillableMinutes, nodes);
        }).ToArray();
        return new(result, query.Page, query.PageSize, count, (int)Math.Ceiling(count / (double)query.PageSize));
    }
    private async Task<List<PeriodProjectRow>> PeriodProjects(long[] ids,bool team,CancellationToken ct) =>
        await (from w in Visible(team).Where(w=>ids.Contains(w.PeriodId))
               join p in db.Projects on w.ProjectId equals p.Id
               group w by new {w.PeriodId,w.ProjectId,p.DisplayName} into g
               select new PeriodProjectRow(g.Key.PeriodId,new PeriodProjectDto(g.Key.ProjectId,g.Key.DisplayName,
                   g.Sum(w=>(long)w.DurationMinutes),g.Sum(w=>w.Billable?(long)w.DurationMinutes:0)))).ToListAsync(ct);
    private sealed record PeriodProjectRow(long PeriodId,PeriodProjectDto Project);
    private async Task<TimesheetPeriod> Period(DateOnly date,CancellationToken ct) {
        var start=Monday(date);var p=await db.TimesheetPeriods.SingleOrDefaultAsync(p=>p.TenantId==actor.TenantId&&p.UserId==actor.UserId&&p.PeriodStart==start,ct);
        if(p is null){p=new(){TenantId=actor.TenantId,UserId=actor.UserId,PeriodStart=start,PeriodEnd=start.AddDays(6)};db.TimesheetPeriods.Add(p);await db.SaveChangesAsync(ct);}
        Editable(p);return p;
    }
    private static void Editable(TimesheetPeriod p){if(p.Status is "SUBMITTED" or "APPROVED")throw new ProjectConflictException("Submitted or approved timesheets cannot be changed.");}
    private async Task Capacity(DateOnly date,int minutes,long exclude,CancellationToken ct) {
        var used=await db.WorkLogs.Where(w=>w.TenantId==actor.TenantId&&w.UserId==actor.UserId&&w.WorkDate==date&&w.Id!=exclude).SumAsync(w=>w.DurationMinutes,ct);
        if(used+minutes>1440)throw new ArgumentException("Daily work cannot exceed 1440 minutes.");
    }
    private async Task<Guid> Event(string action,string type,long id,object? before,object? after,CancellationToken ct) {
        var e=new IssueAuditEvent{EventId=Guid.NewGuid(),EventType=action,TenantId=actor.TenantId,EntityType=type,EntityId=id,EntityKey=$"{type}-{id}",PerformedByUserId=actor.UserId,OccurredAt=DateTimeOffset.UtcNow,
            OldValues=before==null?null:JsonSerializer.Serialize(before),NewValues=after==null?null:JsonSerializer.Serialize(after)};
        await outbox.AddAsync(e.EventId,action,"aiworkhub.issue.audit",JsonSerializer.Serialize(e),e.OccurredAt,ct);return e.EventId;
    }
    public async Task<WorkLogDto> Create(WorkLogRequest request,CancellationToken ct) {
        Auth();WorkLog? w=null;
        await unit.ExecuteInTransactionAsync(async token=>{
            await Lock(token);
            var issue=await scope.Issues(actor.TenantId).SingleOrDefaultAsync(i=>i.Id==request.IssueId,token)??throw new ResourceNotFoundException();
            w=new(){TenantId=actor.TenantId,UserId=actor.UserId,IssueId=issue.Id,ProjectId=issue.ProjectId};
            w.Update(request.WorkDate,request.DurationMinutes,request.Description,request.Billable,request.StartTime,request.EndTime);
            var period=await Period(w.WorkDate,token);w.PeriodId=period.Id;period.Changed();await Capacity(w.WorkDate,w.DurationMinutes,0,token);
            issue.AdjustLoggedTime(w.DurationMinutes);
            db.WorkLogs.Add(w);await db.SaveChangesAsync(token);await Event("WORKLOG_CREATED","WORKLOG",w.Id,null,w,token);await db.SaveChangesAsync(token);
        },ct);return await Get(w!.Id,false,ct);
    }
    public async Task<WorkLogDto> Update(long id,WorkLogRequest request,CancellationToken ct) {
        Auth();await unit.ExecuteInTransactionAsync(async token=>{
            await Lock(token);var w=await Visible().SingleOrDefaultAsync(w=>w.Id==id,token)??throw new ResourceNotFoundException();precondition.Check(w.Version);
            if(request.IssueId!=w.IssueId)throw new ArgumentException("A work log cannot be moved to another issue.");
            var before=await Get(id,false,token);var old=await db.TimesheetPeriods.SingleAsync(p=>p.Id==w.PeriodId,token);Editable(old);
            var priorMinutes=w.DurationMinutes;
            w.Update(request.WorkDate,request.DurationMinutes,request.Description,request.Billable,request.StartTime,request.EndTime);
            var issue=await scope.Issues(actor.TenantId).SingleAsync(i=>i.Id==w.IssueId,token);issue.AdjustLoggedTime(w.DurationMinutes-priorMinutes);var period=await Period(w.WorkDate,token);
            await Capacity(w.WorkDate,w.DurationMinutes,id,token);w.PeriodId=period.Id;old.Changed();period.Changed();
            await db.SaveChangesAsync(token);await Event("WORKLOG_UPDATED","WORKLOG",id,before,w,token);await db.SaveChangesAsync(token);
        },ct);return await Get(id,false,ct);
    }
    public async Task Delete(long id,CancellationToken ct) {
        Auth();await unit.ExecuteInTransactionAsync(async token=>{
            await Lock(token);var w=await Visible().SingleOrDefaultAsync(w=>w.Id==id,token)??throw new ResourceNotFoundException();precondition.Check(w.Version);
            var p=await db.TimesheetPeriods.SingleAsync(p=>p.Id==w.PeriodId,token);Editable(p);p.Changed();
            var issue=await scope.Issues(actor.TenantId).SingleAsync(i=>i.Id==w.IssueId,token);issue.AdjustLoggedTime(-w.DurationMinutes);
            await Event("WORKLOG_DELETED","WORKLOG",id,w,null,token);db.WorkLogs.Remove(w);await db.SaveChangesAsync(token);
        },ct);
    }
    public async Task<IReadOnlyList<TimesheetApprover>> Approvers(long periodId,CancellationToken ct) {
        var p=await VisiblePeriods().SingleOrDefaultAsync(p=>p.Id==periodId,ct)??throw new ResourceNotFoundException();
        return (await directory.ListAsync(ct)).Where(u=>u.Id!=p.UserId).ToArray();
    }
    public async Task<PeriodDto> Act(string action,PeriodAction request,CancellationToken ct) {
        Auth();long id=0;
        await unit.ExecuteInTransactionAsync(async token=>{
            await Lock(token);var p=await VisiblePeriods(action!="submit").SingleOrDefaultAsync(p=>p.Id==request.PeriodId,token)??throw new ResourceNotFoundException();
            precondition.Check(p.Version);var before=JsonSerializer.SerializeToElement(p);
            var logs=db.WorkLogs.Where(w=>w.TenantId==actor.TenantId&&w.PeriodId==p.Id);var visible=scope.Issues(actor.TenantId);
            if(!await logs.AnyAsync(token)||await logs.AnyAsync(w=>!visible.Any(i=>i.Id==w.IssueId),token))throw new ResourceNotFoundException();
            long recipient;
            if(action=="submit") {
                if(p.UserId!=actor.UserId)throw new ResourceNotFoundException();Editable(p);
                var approvers=await Approvers(p.Id,token);
                if(!approvers.Any(a=>a.Id==request.ApproverUserId))throw new ArgumentException("Choose an eligible approver with access to every issue in this period.");
                var reviewer=approvers.Single(a=>a.Id==request.ApproverUserId);
                var reviewerContext=new TenantContext.TenantContext();reviewerContext.Set(reviewer.Id,actor.TenantId,reviewer.Roles);
                var reviewerIssues=new JiraResourceScope(db,reviewerContext).Issues(actor.TenantId);
                if(await logs.AnyAsync(w=>!reviewerIssues.Any(i=>i.Id==w.IssueId),token))throw new ArgumentException("The selected approver cannot access every issue in this period.");
                p.Status="SUBMITTED";p.SubmittedAt=DateTimeOffset.UtcNow;p.ApproverUserId=request.ApproverUserId;p.RejectionReason=null;p.ApprovedAt=null;p.ApprovedByUserId=null;recipient=p.ApproverUserId!.Value;
            } else {
                if(p.UserId==actor.UserId)throw new ProjectConflictException("You cannot review your own timesheet.");
                if(p.Status!="SUBMITTED")throw new ProjectConflictException("Only submitted timesheets can be reviewed.");
                if(action=="reject"&&string.IsNullOrWhiteSpace(request.Reason))throw new ArgumentException("A rejection reason is required.");
                p.Status=action=="approve"?"APPROVED":"REJECTED";p.RejectionReason=action=="reject"?request.Reason!.Trim():null;
                p.ApprovedByUserId=action=="approve"?actor.UserId:null;p.ApprovedAt=action=="approve"?DateTimeOffset.UtcNow:null;recipient=p.UserId;
            }
            p.Changed();await db.SaveChangesAsync(token);var eventId=await Event("TIMESHEET_"+p.Status,"TIMESHEET",p.Id,before,p,token);
            db.Notifications.Add(new Notification{Id=Guid.NewGuid(),EventId=eventId,TenantId=actor.TenantId,RecipientUserId=recipient,ActorUserId=actor.UserId,IssueId=0,TimesheetPeriodId=p.Id,Kind="TIMESHEET_"+p.Status,Reason=p.RejectionReason,CreatedAt=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync(token);id=p.Id;
        },ct);
        var period=await db.TimesheetPeriods.AsNoTracking().SingleAsync(p=>p.Id==id&&p.TenantId==actor.TenantId,ct);
        var total=await Visible(true).Where(w=>w.PeriodId==id).SumAsync(w=>(long)w.DurationMinutes,ct);
        var billable=await Visible(true).Where(w=>w.PeriodId==id&&w.Billable).SumAsync(w=>(long)w.DurationMinutes,ct);
        var projects=await PeriodProjects([id],true,ct);
        return new(period.Id,period.UserId,period.PeriodStart,period.PeriodEnd,period.Status,period.SubmittedAt,period.ApproverUserId,period.ApprovedByUserId,period.ApprovedAt,period.RejectionReason,period.Version,total,billable,projects.Select(t=>t.Project).ToArray());
    }
}

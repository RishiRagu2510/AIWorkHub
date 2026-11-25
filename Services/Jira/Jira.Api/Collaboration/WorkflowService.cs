using System.Text.Json;
using Jira.Application.Abstractions.Services;
using Jira.Application.Services;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Collaboration;
using Microsoft.EntityFrameworkCore;
using TenantContext;
namespace Jira.Api.Collaboration;
public sealed record WorkflowTransition(string From,string To,string? Permission = null);
public sealed record WorkflowDefinition(string InitialStatus,string[] Statuses,WorkflowTransition[] Transitions);
public sealed class WorkflowService(JiraDbContext db,JiraResourceScope scope,IHttpContextAccessor http):IWorkflowValidator
{
    public static WorkflowDefinition Default => new("TODO",["TODO","IN_PROGRESS","CODE_REVIEW","TESTING","DONE","BLOCKED","CANCELLED"],[
        new("TODO","IN_PROGRESS"),new("IN_PROGRESS","CODE_REVIEW"),new("CODE_REVIEW","TESTING"),new("TESTING","DONE")]);
    public async Task<WorkflowDefinition> Definition(long tenant,long project,CancellationToken ct)
    {
        var json=await db.WorkflowRules.Where(w=>w.TenantId==tenant && w.ProjectId==project).Select(w=>w.Definition).SingleOrDefaultAsync(ct);
        return json is null?Default:JsonSerializer.Deserialize<WorkflowDefinition>(json)!;
    }
    public async Task ValidateAsync(long tenantId,long projectId,string? from,string to,CancellationToken ct,bool confirmSkip=false)
    {
        
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenantId})",ct);
        var definition=await Definition(tenantId,projectId,ct); to=to.Trim().ToUpperInvariant(); from=from?.Trim().ToUpperInvariant();
        if(from is null) { if(to!=definition.InitialStatus) throw new ArgumentException("New issues must use the workflow initial status."); return; }
        if(from==to) return;
        if(!await db.WorkflowRules.AnyAsync(w=>w.TenantId==tenantId&&w.ProjectId==projectId,ct))
        { Jira.Application.Services.WorkflowRules.Validate(from,to,confirmSkip); return; }
        var transition=definition.Transitions.SingleOrDefault(t=>t.From==from && t.To==to);
        if(transition is null) throw new ProjectConflictException("This status transition is not allowed by the project workflow.");
        if(transition.Permission is not null && !http.HttpContext!.User.FindAll(TenantClaimTypes.Permission).Any(p=>p.Value==transition.Permission))
            throw new CollaborationForbiddenException();
    }
    public async Task<object> Get(long tenant,long project,CancellationToken ct)
    {
        if(!await scope.Projects(tenant).AnyAsync(p=>p.Id==project,ct)) throw new ResourceNotFoundException();
        var saved=await db.WorkflowRules.AsNoTracking().SingleOrDefaultAsync(w=>w.TenantId==tenant && w.ProjectId==project,ct);
        return new {definition=await Definition(tenant,project,ct),version=saved?.Version??0,isDefault=saved is null};
    }
    public async Task Put(long tenant,long project,WorkflowDefinition definition,uint expected,CancellationToken ct)
    {
        if(definition.Statuses is null || definition.Transitions is null || definition.Statuses.Length is <1 or >30 || definition.Transitions.Length>200 ||
            definition.Statuses.Any(s=>string.IsNullOrWhiteSpace(s) || s.Length>50 || s!=s.Trim().ToUpperInvariant()) ||
            definition.Statuses.Distinct().Count()!=definition.Statuses.Length || !definition.Statuses.Contains(definition.InitialStatus) ||
            definition.Transitions.Any(t=>!definition.Statuses.Contains(t.From)||!definition.Statuses.Contains(t.To)||t.From==t.To || t.Permission?.Length>150) ||
            definition.Transitions.Select(t=>(t.From,t.To)).Distinct().Count()!=definition.Transitions.Length)
            throw new ArgumentException("Invalid workflow statuses or transitions.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenant})",ct);
        if(!await scope.Projects(tenant).AnyAsync(p=>p.Id==project,ct)) throw new ResourceNotFoundException();
        var saved=await db.WorkflowRules.SingleOrDefaultAsync(w=>w.TenantId==tenant && w.ProjectId==project,ct);
        if((saved?.Version??0)!=expected) throw new StaleWriteException();
        if(await db.Issues.AnyAsync(i=>i.TenantId==tenant && i.ProjectId==project && !i.IsDeleted && !definition.Statuses.Contains(i.StatusCode),ct))
            throw new ProjectConflictException("The workflow must retain statuses used by existing issues.");
        if(saved is null) {saved=new Jira.Infrastructure.Persistence.Collaboration.WorkflowRules{TenantId=tenant,ProjectId=project};db.WorkflowRules.Add(saved);}
        saved.Definition=JsonSerializer.Serialize(definition);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
public sealed class CollaborationForbiddenException:Exception;

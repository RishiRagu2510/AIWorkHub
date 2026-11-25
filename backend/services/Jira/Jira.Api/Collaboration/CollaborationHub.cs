using System.Runtime.CompilerServices;
using Authorization;
using Jira.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TenantContext;
namespace Jira.Api.Collaboration;
[Authorize]
[RequirePermission("ISSUE_VIEW")]
public sealed class CollaborationHub(IServiceScopeFactory scopes,IPresenceStore presence):Hub
{
    private TenantContext.TenantContext Identity(IServiceProvider services)
    {
        var users=Context.User!.FindAll(TenantClaimTypes.UserId).ToArray();var tenants=Context.User.FindAll(TenantClaimTypes.TenantId).ToArray();
        if(users.Length!=1||tenants.Length!=1||!long.TryParse(users[0].Value,out var user)||user<=0||!long.TryParse(tenants[0].Value,out var tenant)||tenant<=0)throw new HubException("Invalid identity.");
        var context=services.GetRequiredService<TenantContext.TenantContext>();context.Set(user,tenant,Context.User.FindAll(TenantClaimTypes.Role).Select(c=>c.Value));return context;
    }
    public async Task Pulse(long issueId,bool editing=false,bool typing=false)
    {
        await using var scope=scopes.CreateAsyncScope();var identity=Identity(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<CollaborationService>().RequireIssue(issueId,Context.ConnectionAborted);
        if(editing&&!Context.User!.HasClaim(TenantClaimTypes.Permission,"ISSUE_UPDATE")||typing&&!Context.User!.HasClaim(TenantClaimTypes.Permission,"ISSUE_COMMENT_CREATE"))throw new HubException("Permission denied.");
        await presence.Pulse(identity.TenantId,issueId,Context.ConnectionId,new(identity.UserId,identity.Roles.ToArray(),editing,typing?DateTimeOffset.UtcNow.AddSeconds(5):DateTimeOffset.MinValue));
    }
    public IAsyncEnumerable<object> WatchIssue(long issueId,CancellationToken ct)=>Watch("ISSUE",issueId,ct);
    public IAsyncEnumerable<object> WatchBoard(long boardId,CancellationToken ct)=>Watch("BOARD",boardId,ct);
    public IAsyncEnumerable<object> WatchActivity(CancellationToken ct)
    {
        if(!Context.User!.IsInRole("ADMIN")||!Context.User.HasClaim(TenantClaimTypes.Permission,"AUDIT_VIEW"))throw new HubException("Permission denied.");
        return Watch("ACTIVITY",0,ct);
    }
    private async IAsyncEnumerable<object> Watch(string kind,long id,[EnumeratorCancellation] CancellationToken cancellation)
    {
        var key=$"watch:{kind}:{id}";
        lock(Context.Items){if(Context.Items.ContainsKey(key)||Context.Items.Count>=10)throw new HubException("Stream already active or limit reached.");Context.Items[key]=true;}
        try
        {
            using var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation,Context.ConnectionAborted);string? previous=null;
            while(!stop.IsCancellationRequested)
            {
                await using var scope=scopes.CreateAsyncScope();var identity=Identity(scope.ServiceProvider);
                var service=scope.ServiceProvider.GetRequiredService<CollaborationService>();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
                var resource=scope.ServiceProvider.GetRequiredService<JiraResourceScope>();var events=service.Activities();
                if(kind=="ISSUE") {await service.RequireIssue(id,stop.Token);events=events.Where(e=>e.EntityType=="ISSUE"&&e.EntityId==id);}
                if(kind=="BOARD") {var board=await service.RequireBoard(id,stop.Token);events=events.Where(e=>e.ProjectId==board.ProjectId);}
                var stamp=await events.GroupBy(e=>e.TenantId).Select(g=>new{count=g.Count(),last=g.Max(e=>e.OccurredAt)}).SingleOrDefaultAsync(stop.Token);
                var people=new List<object>();
                if(kind=="ISSUE")
                {
                    foreach(var group in (await presence.List(identity.TenantId,id)).GroupBy(p=>p.UserId))
                    {
                        var entry=group.First();var viewer=new TenantContext.TenantContext();viewer.Set(entry.UserId,identity.TenantId,entry.Roles);
                        if(await new JiraResourceScope(db,viewer).Issues(identity.TenantId).AnyAsync(i=>i.Id==id,stop.Token))
                            people.Add(new{userId=entry.UserId,editing=group.Any(p=>p.Editing),typing=group.Any(p=>p.TypingUntil>DateTimeOffset.UtcNow)});
                    }
                }
                var serialized=System.Text.Json.JsonSerializer.Serialize(new{stamp,people});
                if(serialized!=previous){previous=serialized;yield return new{changed=true,presence=people};}
                await Task.Delay(TimeSpan.FromSeconds(1),stop.Token);
            }
        }
        finally{lock(Context.Items)Context.Items.Remove(key);}
    }
}

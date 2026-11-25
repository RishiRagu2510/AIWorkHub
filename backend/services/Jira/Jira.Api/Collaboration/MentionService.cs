using System.Text.RegularExpressions;
using Jira.Application.Abstractions.Services;
using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Jira.Api.Collaboration;
public sealed class MentionService(JiraDbContext db,IDeveloperDirectory directory):IMentionService
{
    public async Task NotifyAsync(Issue issue,long actor,string body,IReadOnlyList<long> users,Guid eventId,CancellationToken ct)
    {
        if(users is null||users.Count>10)throw new ArgumentException("At most 10 mention recipients are allowed.");
        var parsed=Regex.Matches(body,@"@\[(\d+)\]",RegexOptions.None,TimeSpan.FromMilliseconds(100)).Select(m=>long.TryParse(m.Groups[1].Value,out var id)?id:0);
        var ids=users.Concat(parsed).Distinct().ToArray();if(ids.Length>10||ids.Any(id=>id<=0))throw new ArgumentException("Invalid mention recipients.");
        foreach(var id in ids)
        {
            var user=await directory.GetCollaborationUserAsync(issue.TenantId,id,ct);
            if(user is null)throw new ArgumentException("A mentioned user is not available for this issue.");
            var context=new TenantContext.TenantContext();context.Set(id,issue.TenantId,user.Roles);
            if(!await new JiraResourceScope(db,context).Issues(issue.TenantId).AnyAsync(i=>i.Id==issue.Id,ct))throw new ArgumentException("A mentioned user is not available for this issue.");
            if(id==actor)continue;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications (id,event_id,tenant_id,recipient_user_id,actor_user_id,issue_id,kind,created_at)
                VALUES ({Guid.NewGuid()},{eventId},{issue.TenantId},{id},{actor},{issue.Id},'ISSUE_MENTIONED',{DateTimeOffset.UtcNow})
                ON CONFLICT (tenant_id,event_id,recipient_user_id) DO NOTHING
                """,ct);
        }
    }
}

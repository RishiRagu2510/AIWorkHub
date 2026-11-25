using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;
using Authorization;
using Jira.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TenantContext;
namespace Jira.Api.Notifications;



[Authorize]
[RequirePermission("ISSUE_VIEW")]
public sealed class NotificationsHub(IServiceScopeFactory scopes) : Hub
{
    public async IAsyncEnumerable<object> Watch([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userClaims = Context.User!.FindAll(TenantClaimTypes.UserId).ToArray();
        var tenantClaims = Context.User.FindAll(TenantClaimTypes.TenantId).ToArray();
        if (userClaims.Length != 1 || tenantClaims.Length != 1 ||
            !long.TryParse(userClaims[0].Value, out var userId) || userId <= 0 ||
            !long.TryParse(tenantClaims[0].Value, out var tenantId) || tenantId <= 0)
            throw new HubException("A valid user and tenant are required.");
        lock (Context.Items)
        {
            if (Context.Items.ContainsKey("notification-stream")) throw new HubException("Only one notification stream is allowed per connection.");
            Context.Items["notification-stream"] = true;
        }
        try
        {
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Context.ConnectionAborted);
            string? previous = null;
            string? previousIssues = null;
            while (!stop.IsCancellationRequested)
            {
                string stamp;
                string issueStamp;
                await using (var scope = scopes.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
                    var identity = new TenantContext.TenantContext();
                    identity.Set(userId, tenantId, Context.User.FindAll(ClaimTypes.Role).Select(c => c.Value));
                    var resources = new JiraResourceScope(db, identity);
                    var visible = await resources.Issues(tenantId).AsNoTracking().OrderBy(i => i.Id).Select(i => new { i.Id, i.Version }).ToListAsync(stop.Token);
                    issueStamp = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";", visible.Select(i => $"{i.Id}:{i.Version}")))));
                    var aggregate = await db.Notifications.AsNoTracking()
                        .Where(n => n.TenantId == tenantId && n.RecipientUserId == userId)
                        .GroupBy(n => n.RecipientUserId)
                        .Select(g => new { Count = g.Count(), Unread = g.Count(n => n.ReadAt == null),
                            Created = g.Max(n => n.CreatedAt), Read = g.Max(n => n.ReadAt) })
                        .SingleOrDefaultAsync(stop.Token);
                    stamp = aggregate is null ? "empty" : $"{aggregate.Count}:{aggregate.Unread}:{aggregate.Created:O}:{aggregate.Read:O}";
                }
                if (stamp != previous || issueStamp != previousIssues)
                {
                    var hint = new { changed = stamp != previous, issuesChanged = issueStamp != previousIssues };
                    previous = stamp; previousIssues = issueStamp;
                    yield return hint;
                }
                await Task.Delay(TimeSpan.FromSeconds(2), stop.Token);
            }
        }
        finally
        {
            lock (Context.Items) Context.Items.Remove("notification-stream");
        }
    }
}

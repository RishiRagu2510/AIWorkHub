using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Jira.Api.Collaboration;
public sealed class AttachmentCleanup(IServiceScopeFactory scopes, IConfiguration config, ILogger<AttachmentCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                if (!string.IsNullOrWhiteSpace(config["Attachments:AccessKey"])) {
                    await using var scope = scopes.CreateAsyncScope();
                    await RunOnce(scope.ServiceProvider.GetRequiredService<JiraDbContext>(), scope.ServiceProvider.GetRequiredService<IAttachmentStorage>(), ct);
                }
                await using (var scope = scopes.CreateAsyncScope()) {
                    var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
                    await db.Set<UserPreference>().Where(p => p.Key.StartsWith("issue-draft:") && p.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-7)).ExecuteDeleteAsync(ct);
                }
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Attachment and draft maintenance will retry."); }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
    public static async Task RunOnce(JiraDbContext db, IAttachmentStorage storage, CancellationToken ct) {
        await foreach (var key in storage.OldObjects(ct)) {
            var parts = key.Split('/');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var tenant) || tenant <= 0 || !Guid.TryParseExact(parts[1], "N", out var id)) continue;
            if (!await db.Set<AttachmentRecord>().AnyAsync(a => a.TenantId == tenant && a.Id == id, ct)) await storage.Remove(key, ct);
        }
    }
}

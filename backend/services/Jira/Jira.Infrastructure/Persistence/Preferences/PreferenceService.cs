using System.Text.Json;
using System.Text.RegularExpressions;
using Jira.Application.Services;
using Jira.Contracts.Collaboration;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Jira.Infrastructure.Persistence.Preferences;

public enum PreferenceOutcome { Saved, Invalid, TooMany }

public sealed record PreferenceSaveResult(PreferenceOutcome Outcome, PreferenceDto? Preference = null);





public sealed class PreferenceService(JiraDbContext db, ITenantContext context)
{
    public const int MaxPreferences = 100;
    private static readonly Regex ValidKey = new(@"^[a-zA-Z0-9:_-]{1,160}$");

    public async Task<IReadOnlyList<PreferenceDto>> Read(CancellationToken ct)
    {
        var records = await db.Set<UserPreference>().AsNoTracking().Where(p => p.TenantId == context.TenantId && p.UserId == context.UserId).ToListAsync(ct);
        return records.Select(p => new PreferenceDto(p.Key, JsonSerializer.Deserialize<JsonElement>(p.Value), p.Version)).ToList();
    }

    public async Task<PreferenceSaveResult> Save(string key, UpdatePreferenceRequest request, CancellationToken ct)
    {
        if (!ValidKey.IsMatch(key) || request.Value.GetRawText().Length > 65536) return new(PreferenceOutcome.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-context.TenantId})", ct);
        await db.Set<UserPreference>().Where(p => p.TenantId == context.TenantId && p.UserId == context.UserId && p.Key.StartsWith("issue-draft:") && p.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-7)).ExecuteDeleteAsync(ct);
        var record = await db.Set<UserPreference>().SingleOrDefaultAsync(p => p.TenantId == context.TenantId && p.UserId == context.UserId && p.Key == key, ct);
        if ((record?.Version ?? 0) != request.Version) throw new StaleWriteException();
        if (record is null)
        {
            if (await db.Set<UserPreference>().CountAsync(p => p.TenantId == context.TenantId && p.UserId == context.UserId, ct) >= MaxPreferences) return new(PreferenceOutcome.TooMany);
            record = new() { TenantId = context.TenantId, UserId = context.UserId, Key = key };
            db.Add(record);
        }
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.Value = request.Value.GetRawText();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(PreferenceOutcome.Saved, new PreferenceDto(record.Key, request.Value, record.Version));
    }
}

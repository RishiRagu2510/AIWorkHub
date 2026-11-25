using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class TenantPlanStore(IdentityTenantDbContext db) : ITenantPlanStore
{
    public Task<TenantSettings?> FindSettingsAsync(long tenantId, CancellationToken ct) =>
        db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

    public Task<TenantSettings?> FindSettingsReadOnlyAsync(long tenantId, CancellationToken ct) =>
        db.TenantSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

    public void AddSettings(TenantSettings settings) => db.TenantSettings.Add(settings);

    public Task<bool> TenantExistsAsync(long tenantId, CancellationToken ct) =>
        db.Tenants.AnyAsync(t => t.Id == tenantId, ct);

    public Task<string?> TenantNameAsync(long tenantId, CancellationToken ct) =>
        db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => (string?)t.Name).FirstOrDefaultAsync(ct);

    public Task<string?> UserDisplayNameAsync(long tenantId, long userId, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == userId && u.TenantId == tenantId).Select(u => (string?)u.DisplayName).FirstOrDefaultAsync(ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

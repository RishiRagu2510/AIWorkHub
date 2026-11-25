using IdentityTenant.Domain.Entities;

namespace IdentityTenant.Application.Abstractions.Persistence;





public interface ITenantPlanStore
{
    
    Task<TenantSettings?> FindSettingsAsync(long tenantId, CancellationToken ct);

    
    Task<TenantSettings?> FindSettingsReadOnlyAsync(long tenantId, CancellationToken ct);

    
    void AddSettings(TenantSettings settings);

    Task<bool> TenantExistsAsync(long tenantId, CancellationToken ct);

    Task<string?> TenantNameAsync(long tenantId, CancellationToken ct);

    
    Task<string?> UserDisplayNameAsync(long tenantId, long userId, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}

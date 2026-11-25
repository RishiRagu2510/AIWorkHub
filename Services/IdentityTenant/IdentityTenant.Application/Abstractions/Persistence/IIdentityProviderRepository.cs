using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;

namespace IdentityTenant.Application.Abstractions.Persistence;

public interface IIdentityProviderRepository
{
    Task<List<IdentityProvider>> GetEnabledGoogleByClientIdAsync(string clientId, CancellationToken cancellationToken);
    Task<IdentityProvider?> GetEnabledByTenantAndTypeAsync(
        long tenantId,
        IdentityProviderType providerType,
        CancellationToken cancellationToken);
}
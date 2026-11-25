using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class IdentityProviderRepository
    : IIdentityProviderRepository
{
    private readonly IdentityTenantDbContext _dbContext;

    public IdentityProviderRepository(
        IdentityTenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<IdentityProvider>> GetEnabledGoogleByClientIdAsync(string clientId, CancellationToken ct) =>
        _dbContext.IdentityProviders.Where(p=>p.ProviderType==IdentityProviderType.Google&&p.Enabled&&p.ClientId==clientId).Take(2).ToListAsync(ct);

    public Task<IdentityProvider?> GetEnabledByTenantAndTypeAsync(
        long tenantId,
        IdentityProviderType providerType,
        CancellationToken cancellationToken)
    {
        return _dbContext.IdentityProviders
            .FirstOrDefaultAsync(
                provider =>
                    provider.TenantId == tenantId &&
                    provider.ProviderType == providerType &&
                    provider.Enabled,
                cancellationToken);
    }
}
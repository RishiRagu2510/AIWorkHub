using IdentityTenant.Domain.Entities;

namespace IdentityTenant.Application.Abstractions.Persistence;

public interface IRoleRepository
{
    Task<Role?> GetByTenantAndCodeAsync(
        long tenantId,
        string code,
        CancellationToken cancellationToken);
}
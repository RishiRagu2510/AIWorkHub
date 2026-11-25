using IdentityTenant.Domain.Entities;

namespace IdentityTenant.Application.Abstractions.Persistence;

public interface ITenantRepository
{
    Task<Tenant?> FindByEmailDomainAsync(string domain, CancellationToken cancellationToken);

    Task<Tenant?> GetByIdAsync(
        long tenantId,
        CancellationToken cancellationToken);

    Task<Tenant?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken);
}
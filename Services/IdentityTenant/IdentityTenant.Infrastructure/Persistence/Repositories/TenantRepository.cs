using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly IdentityTenantDbContext _dbContext;

    public TenantRepository(IdentityTenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Tenant?> FindByEmailDomainAsync(string domain, CancellationToken cancellationToken)
    {
        var normalizedDomain = domain.Trim().ToLowerInvariant();
        return _dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(
            tenant => tenant.EmailDomain != null && tenant.EmailDomain.ToLower() == normalizedDomain,
            cancellationToken);
    }

    public Task<Tenant?> GetByIdAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                tenant => tenant.Id == tenantId,
                cancellationToken);
    }

    public Task<Tenant?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        return _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                tenant => tenant.Slug == slug,
                cancellationToken);
    }
}
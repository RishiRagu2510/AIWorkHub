using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly IdentityTenantDbContext _dbContext;

    public RoleRepository(IdentityTenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Role?> GetByTenantAndCodeAsync(
        long tenantId,
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();

        return _dbContext.Roles
            .FirstOrDefaultAsync(
                role =>
                    role.TenantId == tenantId &&
                    role.Code == normalizedCode,
                cancellationToken);
    }
}
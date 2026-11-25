using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class UserRoleRepository : IUserRoleRepository
{
    private readonly IdentityTenantDbContext _dbContext;

    public UserRoleRepository(IdentityTenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(
        long tenantId,
        long userId,
        long roleId,
        CancellationToken cancellationToken)
    {
        return _dbContext.UserRoles
            .AnyAsync(
                userRole =>
                    userRole.TenantId == tenantId &&
                    userRole.UserId == userId &&
                    userRole.RoleId == roleId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetRoleCodesAsync(
        long tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        return await (
                from userRole in _dbContext.UserRoles.AsNoTracking()
                join role in _dbContext.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where userRole.TenantId == tenantId
                      && userRole.UserId == userId
                      && role.TenantId == tenantId
                select role.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        UserRole userRole,
        CancellationToken cancellationToken)
    {
        await _dbContext.UserRoles.AddAsync(
            userRole,
            cancellationToken);
    }
}
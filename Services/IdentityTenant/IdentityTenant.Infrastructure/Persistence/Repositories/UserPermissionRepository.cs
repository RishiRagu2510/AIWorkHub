using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class UserPermissionRepository
    : IUserPermissionRepository
{
    private readonly IdentityTenantDbContext _dbContext;

    public UserPermissionRepository(
        IdentityTenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(
        long tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        return await (
                from userRole in _dbContext.UserRoles.AsNoTracking()
                join role in _dbContext.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                join rolePermission in _dbContext.RolePermissions.AsNoTracking()
                    on userRole.RoleId equals rolePermission.RoleId
                join permission in _dbContext.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                where userRole.TenantId == tenantId
                      && userRole.UserId == userId
                      && role.TenantId == tenantId
                select permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
    }
}
using IdentityTenant.Domain.Entities;

namespace IdentityTenant.Application.Abstractions.Persistence;

public interface IUserRoleRepository
{
    Task<bool> ExistsAsync(
        long tenantId,
        long userId,
        long roleId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetRoleCodesAsync(
        long tenantId,
        long userId,
        CancellationToken cancellationToken);

    Task AddAsync(
        UserRole userRole,
        CancellationToken cancellationToken);
}
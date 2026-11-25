using IdentityTenant.Domain.Entities;

namespace IdentityTenant.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(
        long userId,
        CancellationToken cancellationToken);

    Task<User?> GetByTenantAndEmailAsync(
        long tenantId,
        string email,
        CancellationToken cancellationToken);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken);
}
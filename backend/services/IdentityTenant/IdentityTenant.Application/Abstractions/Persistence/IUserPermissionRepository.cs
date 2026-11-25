namespace IdentityTenant.Application.Abstractions.Persistence;

public interface IUserPermissionRepository
{
    Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(
        long tenantId,
        long userId,
        CancellationToken cancellationToken);
}
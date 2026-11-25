using IdentityTenant.Domain.Entities;

namespace IdentityTenant.Application.Abstractions.Persistence;

public interface IUserExternalIdentityRepository
{
    Task<UserExternalIdentity?> GetByProviderAndSubjectAsync(
        long identityProviderId,
        string externalSubject,
        CancellationToken cancellationToken);

    Task AddAsync(
        UserExternalIdentity externalIdentity,
        CancellationToken cancellationToken);
}
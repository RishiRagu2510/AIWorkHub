using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class UserExternalIdentityRepository
    : IUserExternalIdentityRepository
{
    private readonly IdentityTenantDbContext _dbContext;

    public UserExternalIdentityRepository(
        IdentityTenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserExternalIdentity?> GetByProviderAndSubjectAsync(
        long identityProviderId,
        string externalSubject,
        CancellationToken cancellationToken)
    {
        return _dbContext.UserExternalIdentities
            .FirstOrDefaultAsync(
                identity =>
                    identity.IdentityProviderId == identityProviderId &&
                    identity.ExternalSubject == externalSubject,
                cancellationToken);
    }

    public async Task AddAsync(
        UserExternalIdentity externalIdentity,
        CancellationToken cancellationToken)
    {
        await _dbContext.UserExternalIdentities.AddAsync(
            externalIdentity,
            cancellationToken);
    }
}
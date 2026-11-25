using IdentityTenant.Domain.Common;

namespace IdentityTenant.Domain.Entities;

public sealed class UserExternalIdentity : Entity
{
    private UserExternalIdentity()
    {
    }

    public UserExternalIdentity(
        long userId,
        long identityProviderId,
        string externalSubject,
        string? email = null)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

        if (identityProviderId <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(identityProviderId));

        if (string.IsNullOrWhiteSpace(externalSubject))
            throw new ArgumentException(
                "External subject is required.",
                nameof(externalSubject));

        UserId = userId;
        IdentityProviderId = identityProviderId;
        ExternalSubject = externalSubject.Trim();
        Email = email?.Trim().ToLowerInvariant();
    }

    public long UserId { get; private set; }

    public long IdentityProviderId { get; private set; }

    public string ExternalSubject { get; private set; } = null!;

    public string? Email { get; private set; }
}
using IdentityTenant.Domain.Common;

namespace IdentityTenant.Domain.Entities;

public sealed class UserSession : Entity
{
    private UserSession()
    {
    }

    public UserSession(
        long userId,
        string sessionTokenHash,
        DateTimeOffset expiresAt, string loginMethod = "GOOGLE")
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

        if (string.IsNullOrWhiteSpace(sessionTokenHash))
            throw new ArgumentException(
                "Session token hash is required.",
                nameof(sessionTokenHash));

        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException(
                "Session must expire in the future.",
                nameof(expiresAt));

        LoginMethod = loginMethod == "PASSWORD" ? "PASSWORD" : "GOOGLE";
        UserId = userId;
        SessionTokenHash = sessionTokenHash.Trim();
        ExpiresAt = expiresAt;
    }

    public string LoginMethod { get; private set; } = "GOOGLE";

    public long UserId { get; private set; }

    public string SessionTokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public bool IsActive =>
        RevokedAt is null &&
        ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke()
    {
        RevokedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void TouchSession()
    {
        LastSeenAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
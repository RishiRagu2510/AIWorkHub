using IdentityTenant.Domain.Common;
using IdentityTenant.Domain.Enums;

namespace IdentityTenant.Domain.Entities;

public sealed class User : Entity
{
    private User()
    {
    }

    public User(
        long tenantId,
        string email,
        string displayName)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "Email is required.",
                nameof(email));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException(
                "Display name is required.",
                nameof(displayName));

        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();

        Status = UserStatus.Active;
        EmailVerified = false;
    }

    public long TenantId { get; private set; }

    public string Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public UserStatus Status { get; private set; }

    public bool EmailVerified { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public void VerifyEmail()
    {
        EmailVerified = true;
        Touch();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException(
                "Display name is required.",
                nameof(displayName));

        DisplayName = displayName.Trim();
        Touch();
    }

    public void Suspend()
    {
        Status = UserStatus.Suspended;
        Touch();
    }

    public void Activate()
    {
        Status = UserStatus.Active;
        Touch();
    }

    public void Disable()
    {
        Status = UserStatus.Disabled;
        Touch();
    }
}
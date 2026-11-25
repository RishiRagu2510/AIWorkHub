namespace IdentityTenant.Domain.Entities;

public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(
        long tenantId,
        long userId,
        long roleId)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));

        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        AssignedAt = DateTimeOffset.UtcNow;
    }

    public long TenantId { get; private set; }

    public long UserId { get; private set; }

    public long RoleId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }
}
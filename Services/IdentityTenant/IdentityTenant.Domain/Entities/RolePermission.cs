namespace IdentityTenant.Domain.Entities;

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(
        long roleId,
        long permissionId)
    {
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));

        if (permissionId <= 0) throw new ArgumentOutOfRangeException(nameof(permissionId));

        RoleId = roleId;
        PermissionId = permissionId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public long RoleId { get; private set; }

    public long PermissionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
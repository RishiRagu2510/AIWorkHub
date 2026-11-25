using IdentityTenant.Domain.Common;

namespace IdentityTenant.Domain.Entities;

public sealed class Role : Entity
{
    private Role()
    {
    }

    public Role(
        long tenantId,
        string code,
        string name,
        string? description = null)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Role code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Role name is required.", nameof(name));

        TenantId = tenantId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
    }

    public long TenantId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public void Update(
        string name,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Role name is required.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();

        Touch();
    }
}
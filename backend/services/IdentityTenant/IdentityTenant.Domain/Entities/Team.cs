using IdentityTenant.Domain.Common;
using IdentityTenant.Domain.Enums;

namespace IdentityTenant.Domain.Entities;

public sealed class Team : Entity
{
    private Team()
    {
    }

    public Team(
        long tenantId,
        string name,
        string code,
        string? description = null)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Team name is required.",
                nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "Team code is required.",
                nameof(code));

        TenantId = tenantId;
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = description?.Trim();
        Status = TeamStatus.Active;
    }

    public long TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public string Code { get; private set; } = null!;

    public string? Description { get; private set; }

    public TeamStatus Status { get; private set; }

    public void Disable()
    {
        Status = TeamStatus.Disabled;
        Touch();
    }

    public void Activate()
    {
        Status = TeamStatus.Active;
        Touch();
    }
}
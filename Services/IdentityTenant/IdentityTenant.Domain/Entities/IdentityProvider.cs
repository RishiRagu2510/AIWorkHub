using IdentityTenant.Domain.Common;
using IdentityTenant.Domain.Enums;

namespace IdentityTenant.Domain.Entities;

public sealed class IdentityProvider : Entity
{
    private IdentityProvider()
    {
    }

    public IdentityProvider(
        long tenantId,
        string providerName,
        IdentityProviderType providerType,
        string clientId,
        string? authority = null)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException(
                "Provider name is required.",
                nameof(providerName));

        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException(
                "Client ID is required.",
                nameof(clientId));

        TenantId = tenantId;
        ProviderName = providerName.Trim();
        ProviderType = providerType;
        ClientId = clientId.Trim();
        Authority = authority?.Trim();
        Enabled = true;
    }

    public long TenantId { get; private set; }

    public string ProviderName { get; private set; } = null!;

    public IdentityProviderType ProviderType { get; private set; }

    public string ClientId { get; private set; } = null!;

    public string? Authority { get; private set; }

    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
        Touch();
    }

    public void Disable()
    {
        Enabled = false;
        Touch();
    }
}
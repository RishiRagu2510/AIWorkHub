using IdentityTenant.Domain.Common;

namespace IdentityTenant.Domain.Entities;

public sealed class TenantSettings : Entity
{
    private TenantSettings()
    {
    }

    public TenantSettings(
        long tenantId,
        string timezone = "UTC",
        string locale = "en-US")
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

        TenantId = tenantId;
        Timezone = string.IsNullOrWhiteSpace(timezone)
            ? "UTC"
            : timezone.Trim();

        Locale = string.IsNullOrWhiteSpace(locale)
            ? "en-US"
            : locale.Trim();

        Settings = new Dictionary<string, object?>();
    }

    public long TenantId { get; private set; }

    public string Timezone { get; private set; } = "UTC";

    public string Locale { get; private set; } = "en-US";

    public Dictionary<string, object?> Settings { get; private set; }
        = new();

    public void Update(
        string timezone,
        string locale,
        Dictionary<string, object?> settings)
    {
        Timezone = string.IsNullOrWhiteSpace(timezone)
            ? "UTC"
            : timezone.Trim();

        Locale = string.IsNullOrWhiteSpace(locale)
            ? "en-US"
            : locale.Trim();

        Settings = settings ?? new Dictionary<string, object?>();

        Touch();
    }
}
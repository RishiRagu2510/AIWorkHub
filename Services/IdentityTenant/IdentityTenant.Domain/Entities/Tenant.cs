using IdentityTenant.Domain.Common;
using IdentityTenant.Domain.Enums;

namespace IdentityTenant.Domain.Entities;

public sealed class Tenant : Entity
{
    private Tenant()
    {
    }

    public Tenant(
        string name,
        string slug,
        string? emailDomain = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tenant name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Tenant slug is required.", nameof(slug));

        EmailDomain = emailDomain is null ? null : NormalizeEmailDomain(emailDomain);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Status = TenantStatus.Active;
    }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;


    public string? EmailDomain { get; private set; }

    public TenantStatus Status { get; private set; }

    public void SetEmailDomain(string emailDomain)
    {
        EmailDomain = NormalizeEmailDomain(emailDomain);
        Touch();
    }

    private static string NormalizeEmailDomain(string domain)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        if (normalized.Length > 253 || !normalized.Contains('.') ||
            Uri.CheckHostName(normalized) != UriHostNameType.Dns || normalized.EndsWith('.'))
            throw new ArgumentException("A valid company email domain is required.", nameof(domain));
        return normalized;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tenant name is required.", nameof(name));

        Name = name.Trim();
        Touch();
    }

    public void Suspend()
    {
        Status = TenantStatus.Suspended;
        Touch();
    }

    public void Activate()
    {
        Status = TenantStatus.Active;
        Touch();
    }

    public void Disable()
    {
        Status = TenantStatus.Disabled;
        Touch();
    }
}
using System.Security.Claims;
using TenantContext;

namespace Authorization;

public static class FeatureLicense
{
    public const string Athenna = "athenna";   
    public const string None = "none";         

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase) { Athenna };

    public static bool IsKnown(string feature) => Known.Contains(feature.Trim());

    
    public static string? RequiredPortal(string feature) =>
        string.Equals(feature.Trim(), Athenna, StringComparison.OrdinalIgnoreCase) ? PortalLicense.Hub : null;

    public static bool IsLicensed(ClaimsPrincipal user, string feature) =>
        IsLicensed(user.FindAll(TenantClaimTypes.Feature).Select(c => c.Value), user.FindAll(TenantClaimTypes.Portal).Select(c => c.Value), feature);

    




    public static bool IsLicensed(IEnumerable<string>? licensedFeatures, IEnumerable<string>? licensedPortals, string feature)
    {
        var portal = RequiredPortal(feature);
        if (portal is not null && !PortalLicense.IsLicensed(licensedPortals ?? [], portal)) return false;
        return IsListed(licensedFeatures, feature);
    }

    public static bool IsListed(IEnumerable<string>? licensedFeatures, string feature)
    {
        var licensed = (licensedFeatures ?? [])
            .Select(v => v?.Trim() ?? "")
            .Where(v => v.Length > 0)
            .ToArray();
        if (licensed.Length == 0) return true;
        return licensed.Contains(feature.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}

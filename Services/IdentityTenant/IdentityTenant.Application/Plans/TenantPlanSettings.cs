using System.Text.Json;
using Authorization;

namespace IdentityTenant.Application.Plans;





public static class TenantPlanSettings
{
    public const string PortalsKey = "allowedPortals";
    public const string FeaturesKey = "allowedFeatures";

    
    public static List<string> ReadList(IDictionary<string, object?>? settings, string key)
    {
        var names = new List<string>();
        if (settings is null || !settings.TryGetValue(key, out var raw) || raw is null) return names;
        if (raw is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
                foreach (var item in json.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) names.Add(item.GetString()!);
        }
        else if (raw is IEnumerable<object> list)
        {
            foreach (var item in list)
                if (!string.IsNullOrWhiteSpace(item?.ToString())) names.Add(item!.ToString()!);
        }
        return names;
    }

    
    public static (string[]? Portals, string[]? Features, string? Error) Normalize(IEnumerable<string>? portals, IEnumerable<string>? features)
    {
        string[]? canonicalPortals = null;
        if (portals is not null)
        {
            var requested = portals.Select(p => p?.Trim() ?? "").ToArray();
            if (requested.Length == 0 || requested.Any(p => p.Length == 0))
                return (null, null, "Portals must list at least one portal, or be left out to remove the restriction.");
            var unknown = requested.Where(p => !PortalLicense.IsKnown(p)).ToArray();
            if (unknown.Length > 0)
                return (null, null, $"Unknown portal: {string.Join(", ", unknown)}. Use workspace, employee, timesheet or hub.");
            canonicalPortals = requested.Select(p => PortalLicense.NamesFor(p)[0]).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
        }

        string[]? canonicalFeatures = null;
        if (features is not null)
        {
            var requested = features.Select(f => f?.Trim().ToLowerInvariant() ?? "").ToArray();
            if (requested.Length == 0 || requested.Any(f => f.Length == 0))
                return (null, null, "Features must list at least one entry (use \"none\" for no optional features), or be left out to remove the restriction.");
            var unknown = requested.Where(f => f != FeatureLicense.None && !FeatureLicense.IsKnown(f)).ToArray();
            if (unknown.Length > 0)
                return (null, null, $"Unknown feature: {string.Join(", ", unknown)}. Use athenna, or none.");
            if (requested.Contains(FeatureLicense.None) && requested.Any(f => f != FeatureLicense.None))
                return (null, null, "\"none\" cannot be combined with other features.");
            canonicalFeatures = requested.Distinct().Order().ToArray();
        }
        return (canonicalPortals, canonicalFeatures, null);
    }
}

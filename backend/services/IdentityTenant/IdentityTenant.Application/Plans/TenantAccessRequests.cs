using System.Text.Json;
using Authorization;

namespace IdentityTenant.Application.Plans;





public sealed record AccessRequest(string Id, string Kind, string Name, long UserId, string UserName, string? Note, DateTimeOffset CreatedAt, string Action = TenantAccessRequests.AddAction)
{
    public bool IsCancellation => string.Equals(Action, TenantAccessRequests.RemoveAction, StringComparison.OrdinalIgnoreCase);
}






public static class TenantAccessRequests
{
    public const string Key = "accessRequests";
    public const string PortalKind = "portal";
    public const string FeatureKind = "feature";
    public const string AddAction = "add";
    public const string RemoveAction = "remove";
    public const int MaxOpen = 100;
    public const int MaxNoteLength = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    
    public static readonly string[] Portals = ["workspace", "employee", "timesheet", "hub"];

    public static List<AccessRequest> Read(IDictionary<string, object?>? settings)
    {
        if (settings is null || !settings.TryGetValue(Key, out var raw) || raw is null) return [];
        try
        {
            var element = raw is JsonElement json ? json : JsonSerializer.SerializeToElement(raw, Json);
            return element.ValueKind == JsonValueKind.Array ? element.Deserialize<List<AccessRequest>>(Json) ?? [] : [];
        }
        catch (JsonException)
        {
            return [];   
        }
    }

    public static void Write(IDictionary<string, object?> settings, IReadOnlyCollection<AccessRequest> requests)
    {
        if (requests.Count == 0) settings.Remove(Key);
        else settings[Key] = JsonSerializer.SerializeToElement(requests, Json);
    }

    
    public static string DisplayName(string kind, string name) => (kind, name.Trim().ToLowerInvariant()) switch
    {
        (PortalKind, "hub") => "AIWorkHub",
        (PortalKind, "workspace") => "GridHub",
        (PortalKind, "employee") => "Persona",
        (PortalKind, "timesheet") => "Chronos",
        (FeatureKind, "athenna") => "the Athenna AI chat",
        _ => name,
    };

    
    public static bool IsIncluded(string kind, string name, IEnumerable<string> portals, IEnumerable<string> features) =>
        kind == PortalKind ? PortalLicense.IsLicensed(portals, name) : FeatureLicense.IsLicensed(features.ToArray(), portals.ToArray(), name);

    
    public static List<string> IncludedPortals(IEnumerable<string> portals)
    {
        var p = portals.ToArray();
        return Portals.Where(name => PortalLicense.IsLicensed(p, name)).ToList();
    }

    



    public static List<AccessRequest> StillOpen(IEnumerable<AccessRequest> requests, IEnumerable<string> portals, IEnumerable<string> features)
    {
        var p = portals.ToArray();
        var f = features.ToArray();
        return requests.Where(r => IsIncluded(r.Kind, r.Name, p, f) == r.IsCancellation).ToList();
    }
}

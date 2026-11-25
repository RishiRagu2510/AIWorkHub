using System.Security.Claims;
using TenantContext;

namespace Authorization;

public static class PortalLicense
{
    public const string Workspace = "workspace";   
    public const string Employee = "employee";     
    public const string Timesheet = "timesheet";   
    public const string Hub = "hub";               

    
    private static readonly Dictionary<string, string[]> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        [Workspace] = [Workspace, "gridhub"],
        ["gridhub"] = [Workspace, "gridhub"],
        [Employee] = [Employee, "persona"],
        ["persona"] = [Employee, "persona"],
        [Timesheet] = [Timesheet, "chronos"],
        ["chronos"] = [Timesheet, "chronos"],
        [Hub] = [Hub, "aiworkhub"],
        ["aiworkhub"] = [Hub, "aiworkhub"],
    };

    
    public static IReadOnlyList<string> NamesFor(string portal) =>
        Names.TryGetValue(portal.Trim(), out var names) ? names : [portal.Trim().ToLowerInvariant()];

    public static bool IsKnown(string portal) => Names.ContainsKey(portal.Trim());

    public static bool IsLicensed(ClaimsPrincipal user, string portal) =>
        IsLicensed(user.FindAll(TenantClaimTypes.Portal).Select(c => c.Value), portal);

    



    public static bool IsLicensed(IEnumerable<string> licensedPortals, string portal)
    {
        var licensed = licensedPortals
            .Select(v => v?.Trim() ?? "")
            .Where(v => v.Length > 0)
            .ToArray();
        if (licensed.Length == 0) return true;
        var accepted = NamesFor(portal);
        return licensed.Any(v => accepted.Contains(v, StringComparer.OrdinalIgnoreCase));
    }

    
    
    
    
    private static readonly (string Prefix, string Portal)[] PermissionOwners =
    [
        ("TIMESHEET_", Timesheet),
    ];

    
    public static string? OwningPortal(string permission)
    {
        foreach (var (prefix, portal) in PermissionOwners)
            if (permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return portal;
        return null;
    }

    
    public static bool IsPermissionLicensed(IEnumerable<string> licensedPortals, string permission) =>
        OwningPortal(permission) is not { } owner || IsLicensed(licensedPortals, owner);
}

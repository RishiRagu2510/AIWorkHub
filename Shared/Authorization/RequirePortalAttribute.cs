using Microsoft.AspNetCore.Authorization;

namespace Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePortalAttribute : AuthorizeAttribute
{
    public RequirePortalAttribute(string portal)
    {
        if (string.IsNullOrWhiteSpace(portal) || !PortalLicense.IsKnown(portal))
            throw new ArgumentException($"'{portal}' is not a known portal.", nameof(portal));
        Policy = PermissionPolicyProvider.PortalPrefix + portal.Trim().ToLowerInvariant();
    }
}

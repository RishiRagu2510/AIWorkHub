using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Authorization;

public sealed class PermissionPolicyProvider
    : DefaultAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "Permission:";
    public const string PortalPrefix = "Portal:";
    public const string FeaturePrefix = "Feature:";

    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?>
        GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(FeaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var feature = policyName[FeaturePrefix.Length..];
            return string.IsNullOrWhiteSpace(feature)
                ? null
                : new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new FeatureRequirement(feature))
                    .Build();
        }

        if (policyName.StartsWith(PortalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var portal = policyName[PortalPrefix.Length..];
            return string.IsNullOrWhiteSpace(portal)
                ? null
                : new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PortalRequirement(portal))
                    .Build();
        }

        if (!policyName.StartsWith(
                PolicyPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var permissionCode =
            policyName[PolicyPrefix.Length..];

        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new PermissionRequirement(permissionCode))
            .Build();
    }
}
using Microsoft.AspNetCore.Authorization;

namespace Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireFeatureAttribute : AuthorizeAttribute
{
    public RequireFeatureAttribute(string feature)
    {
        if (string.IsNullOrWhiteSpace(feature) || !FeatureLicense.IsKnown(feature))
            throw new ArgumentException($"'{feature}' is not a known feature.", nameof(feature));
        Policy = PermissionPolicyProvider.FeaturePrefix + feature.Trim().ToLowerInvariant();
    }
}

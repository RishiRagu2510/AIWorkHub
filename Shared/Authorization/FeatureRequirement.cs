using Microsoft.AspNetCore.Authorization;

namespace Authorization;

public sealed class FeatureRequirement : IAuthorizationRequirement
{
    public FeatureRequirement(string feature)
    {
        if (string.IsNullOrWhiteSpace(feature)) throw new ArgumentException("Feature is required.", nameof(feature));
        Feature = feature.Trim().ToLowerInvariant();
    }

    public string Feature { get; }
}

public sealed class FeatureAuthorizationHandler : AuthorizationHandler<FeatureRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (FeatureLicense.IsLicensed(context.User, requirement.Feature)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

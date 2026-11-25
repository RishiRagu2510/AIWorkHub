using Microsoft.AspNetCore.Authorization;

namespace Authorization;

public sealed class PortalRequirement : IAuthorizationRequirement
{
    public PortalRequirement(string portal)
    {
        if (string.IsNullOrWhiteSpace(portal)) throw new ArgumentException("Portal is required.", nameof(portal));
        Portal = portal.Trim().ToLowerInvariant();
    }

    public string Portal { get; }
}

public sealed class PortalAuthorizationHandler : AuthorizationHandler<PortalRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PortalRequirement requirement)
    {
        if (PortalLicense.IsLicensed(context.User, requirement.Portal)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

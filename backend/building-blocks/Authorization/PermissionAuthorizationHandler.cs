using Microsoft.AspNetCore.Authorization;
using TenantContext;

namespace Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var hasPermission =
            context.User
                .FindAll(TenantClaimTypes.Permission)
                .Any(claim =>
                    requirement.PermissionCode.Split('|').Any(code => string.Equals(
                        claim.Value, code, StringComparison.OrdinalIgnoreCase)));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
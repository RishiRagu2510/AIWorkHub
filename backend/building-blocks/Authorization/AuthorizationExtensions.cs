using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Authorization;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IAuthorizationPolicyProvider,
            PermissionPolicyProvider>();

        services.AddSingleton<
            IAuthorizationHandler,
            PermissionAuthorizationHandler>();

        services.AddSingleton<
            IAuthorizationHandler,
            PortalAuthorizationHandler>();

        services.AddSingleton<
            IAuthorizationHandler,
            FeatureAuthorizationHandler>();

        return services;
    }
}
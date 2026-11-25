using System.Security.Claims;
using TenantContext;

namespace Jira.Api.Middleware;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        TenantContext.TenantContext tenantContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userIdValue =
                httpContext.User.FindFirstValue(
                    TenantClaimTypes.UserId);

            var tenantIdValue =
                httpContext.User.FindFirstValue(
                    TenantClaimTypes.TenantId);

            var roles =
                httpContext.User
                    .FindAll(TenantClaimTypes.Role)
                    .Select(claim => claim.Value);

            if (long.TryParse(userIdValue, out var userId) &&
                long.TryParse(tenantIdValue, out var tenantId) && userId > 0 && tenantId > 0 &&
                httpContext.User.FindAll(TenantClaimTypes.UserId).Count() == 1 &&
                httpContext.User.FindAll(TenantClaimTypes.TenantId).Count() == 1)
            {
                tenantContext.Set(
                    userId,
                    tenantId,
                    roles);
            }
        }

        await _next(httpContext);
    }
}
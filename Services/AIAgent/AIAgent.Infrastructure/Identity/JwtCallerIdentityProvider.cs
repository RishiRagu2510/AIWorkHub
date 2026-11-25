using System.Security.Claims;
using AIAgent.Application.Interfaces;
using Authentication;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Identity;

public sealed class JwtCallerIdentityProvider(
    IHttpContextAccessor httpContextAccessor) : ICallerIdentityProvider
{
    public bool HasCredentials =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true ||
            !TryGetSinglePositiveId(principal, AiWorkHubClaimTypes.UserId, out var userId) ||
            !TryGetSinglePositiveId(principal, AiWorkHubClaimTypes.TenantId, out var tenantId))
            return Task.FromResult<VerifiedCaller?>(null);

        var roles = Values(principal, AiWorkHubClaimTypes.Role);
        if (roles.Length == 0)
            return Task.FromResult<VerifiedCaller?>(null);

        var caller = new VerifiedCaller(
            userId,
            roles[0],
            roles,
            Values(principal, AiWorkHubClaimTypes.Permission),
            tenantId,
            Values(principal, AiWorkHubClaimTypes.Feature),
            Values(principal, AiWorkHubClaimTypes.Portal));

        return Task.FromResult<VerifiedCaller?>(caller);
    }

    private static bool TryGetSinglePositiveId(ClaimsPrincipal principal, string claimType, out long id)
    {
        id = 0;
        var claims = principal.FindAll(claimType).ToArray();
        return claims.Length == 1 &&
               long.TryParse(claims[0].Value, out id) &&
               id > 0;
    }

    private static string[] Values(ClaimsPrincipal principal, string claimType) =>
        principal.FindAll(claimType)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

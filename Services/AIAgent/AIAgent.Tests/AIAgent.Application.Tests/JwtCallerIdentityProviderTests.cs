using System.Security.Claims;
using AIAgent.Infrastructure.Identity;
using Authentication;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;

public sealed class JwtCallerIdentityProviderTests
{
    private static JwtCallerIdentityProvider CreateProvider(bool authenticated = true, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticated ? "Bearer" : null);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        return new JwtCallerIdentityProvider(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public async Task ReadsCallerFromAuthenticatedJwtClaims()
    {
        var provider = CreateProvider(
            true,
            new Claim(AiWorkHubClaimTypes.UserId, "7"),
            new Claim(AiWorkHubClaimTypes.TenantId, "3"),
            new Claim(AiWorkHubClaimTypes.Role, "DEVELOPER"),
            new Claim(AiWorkHubClaimTypes.Role, "EMPLOYEE"),
            new Claim(AiWorkHubClaimTypes.Permission, "ISSUE_VIEW"),
            new Claim(AiWorkHubClaimTypes.Portal, "AIWorkHub"),
            new Claim(AiWorkHubClaimTypes.Feature, "AI_AGENT"));

        var caller = await provider.VerifyAsync();

        Assert.True(provider.HasCredentials);
        Assert.NotNull(caller);
        Assert.Equal(7, caller.UserId);
        Assert.Equal(3, caller.TenantId);
        Assert.Equal("DEVELOPER", caller.Role);
        Assert.Contains("EMPLOYEE", caller.Roles);
        Assert.Contains("ISSUE_VIEW", caller.Permissions);
        Assert.Contains("AIWorkHub", caller.AllowedPortals!);
        Assert.Contains("AI_AGENT", caller.AllowedFeatures!);
    }

    [Fact]
    public async Task DoesNotTrustAnUnauthenticatedPrincipalOrRawBearerHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer forged.token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(AiWorkHubClaimTypes.UserId, "7"),
                new Claim(AiWorkHubClaimTypes.TenantId, "3"),
                new Claim(AiWorkHubClaimTypes.Role, "ADMIN")
            ],
            authenticationType: null));
        var provider = new JwtCallerIdentityProvider(new HttpContextAccessor { HttpContext = context });

        Assert.False(provider.HasCredentials);
        Assert.Null(await provider.VerifyAsync());
    }

    [Theory]
    [InlineData("0", "3", "DEVELOPER")]
    [InlineData("7", "0", "DEVELOPER")]
    [InlineData("7", "3", "")]
    public async Task RequiresValidUserTenantAndRoleClaims(string userId, string tenantId, string role)
    {
        var provider = CreateProvider(
            true,
            new Claim(AiWorkHubClaimTypes.UserId, userId),
            new Claim(AiWorkHubClaimTypes.TenantId, tenantId),
            new Claim(AiWorkHubClaimTypes.Role, role));

        Assert.Null(await provider.VerifyAsync());
    }

    [Fact]
    public async Task RejectsAmbiguousIdentityClaims()
    {
        var provider = CreateProvider(
            true,
            new Claim(AiWorkHubClaimTypes.UserId, "7"),
            new Claim(AiWorkHubClaimTypes.UserId, "8"),
            new Claim(AiWorkHubClaimTypes.TenantId, "3"),
            new Claim(AiWorkHubClaimTypes.Role, "DEVELOPER"));

        Assert.Null(await provider.VerifyAsync());
    }
}

using System.Net;
using Authentication;
using Authorization;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TenantContext;

namespace Backend.Authorization.Tests;

public sealed class IdentityCookieApiTests
{
    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/token")]
    [InlineData("GET", "/api/auth/context")]
    public async Task RepeatedAnonymousApiRequestsDoNotStartGoogleOrSetCookies(string method, string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddAuthentication(IdentityCookieConfiguration.ConfigureAuthentication)
            .AddCookie(options => IdentityCookieConfiguration.ConfigureCookie(options));
        builder.Services.AddAuthorization();
        builder.Services.AddPermissionAuthorization();
        builder.Services.AddScoped<ITenantContext, TenantContext.TenantContext>();
        builder.Services.Configure<JwtSettings>(options => { });
        builder.Services.AddSingleton<IAccessTokenService, UnusedTokenService>();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    private sealed class UnusedTokenService : IAccessTokenService
    {
        public string CreateAccessToken(System.Security.Claims.ClaimsPrincipal principal)
            => throw new InvalidOperationException("Anonymous requests must not issue tokens.");
    }
}

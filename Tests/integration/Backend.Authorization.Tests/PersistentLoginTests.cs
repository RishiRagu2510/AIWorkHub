using System.Net;
using System.Security.Claims;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Configuration;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Backend.Authorization.Tests;

public sealed class PersistentLoginTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    [Fact]
    public async Task PersistentCookieSurvivesNewClientButCannotBeReplayedAfterLogout()
    {
        using var scope = fixture.App.Services.CreateScope();
        var principals = scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>();
        var database = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
        var sessions = new LoginSessionService(database, Options.Create(new LoginSessionOptions()), TimeProvider.System);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(principals);
        builder.Services.AddSingleton(sessions);
        builder.Services.AddAuthentication(IdentityCookieConfiguration.ConfigureAuthentication)
            .AddCookie(options => IdentityCookieConfiguration.ConfigureCookie(options));
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(IdentityTenant.Api.Controllers.AuthController).Assembly);
        builder.Services.AddSingleton(scope.ServiceProvider.GetRequiredService<IdentityTenant.Application.Services.TenantService>());
        builder.Services.AddSingleton(scope.ServiceProvider.GetRequiredService<Authentication.IAccessTokenService>());
        builder.Services.AddSingleton<TenantContext.ITenantContext>(new TenantContext.TenantContext());

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPost("/test-login", async (HttpContext context) =>
        {
            var principal = await principals.SignInGoogleAsync(SecurityFixture.Google("user101@company.com"), default);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        });
        app.MapControllers();
        app.MapPost("/logout", async (HttpContext context) => await context.SignOutAsync());
        await app.StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var response = await client.PostAsync("/test-login", null);
        response.EnsureSuccessStatusCode();
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, cookie => cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, cookie => cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, cookie => cookie.Contains("secure", StringComparison.OrdinalIgnoreCase));
        var cookieHeader = string.Join("; ", cookies.Select(cookie => cookie.Split(';')[0]));
        
        using var restored = app.GetTestClient();
        restored.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        Assert.Equal(HttpStatusCode.OK, (await restored.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await restored.PostAsync("/logout", null)).StatusCode);
        
        Assert.Equal(HttpStatusCode.Unauthorized, (await restored.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionExpiresAtIdleOrAbsoluteDeadline(bool absolute)
    {
        using var scope = fixture.App.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
        var clock = new SessionClock();
        var sessions = new LoginSessionService(database, Options.Create(new LoginSessionOptions()), clock);
        var principal = await scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>()
            .SignInGoogleAsync(SecurityFixture.Google("user101@company.com"), default);
        await sessions.CreateAsync(principal, default);
        Assert.NotNull(await sessions.ValidateAsync(principal, default));
        if (absolute)
        {
            for (var day = 0; day < 7; day++)
            {
                for (var half = 0; half < 2; half++)
                {
                    clock.Now = clock.Now.AddHours(12);
                    var result = await sessions.ValidateAsync(principal, default);
                    if (day == 6 && half == 1) Assert.Null(result);
                    else Assert.NotNull(result);
                }
            }
        }
        else
        {
            clock.Now = clock.Now.AddHours(25);
            Assert.Null(await sessions.ValidateAsync(principal, default));
        }
    }

    [Fact]
    public async Task SessionHandleCannotBeUsedForAnotherUserAndIsNotStoredInPlaintext()
    {
        using var scope = fixture.App.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
        var sessions = new LoginSessionService(database, Options.Create(new LoginSessionOptions()), TimeProvider.System);
        var principal = await scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>()
            .SignInGoogleAsync(SecurityFixture.Google("user101@company.com"), default);
        await sessions.CreateAsync(principal, default);
        var handle = principal.FindFirstValue(LoginSessionService.SessionClaim)!;
        Assert.False(await database.UserSessions.AnyAsync(s => s.SessionTokenHash == handle));
        var impostor = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(LoginSessionService.SessionClaim, handle),
            new Claim(TenantContext.TenantClaimTypes.UserId, "102") }, "test"));
        Assert.Null(await sessions.ValidateAsync(impostor, default));
        await sessions.RevokeAsync(impostor, default);
        Assert.NotNull(await sessions.ValidateAsync(principal, default));
    }

    private sealed class SessionClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

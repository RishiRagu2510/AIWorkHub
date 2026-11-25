using System.Net;
using System.Net.Http.Json;
using Authentication;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Configuration;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TenantContext;

namespace Backend.Authorization.Tests;

public sealed class PasswordAuthHttpTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    [Fact]
    public async Task RegistrationVerificationLoginTokenAndLogoutWorkOverHttp()
    {
        using var scope = fixture.App.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<IdentityTenantDbContext>();
        var principals = services.GetRequiredService<CompanyPrincipalService>();
        var mail = new RecordingMail();
        var passwords = new PasswordLoginService(db, services.GetRequiredService<IMembershipRepository>(),
            services.GetRequiredService<ITenantRepository>(), principals, new PasswordHasher<LocalCredential>(), mail, TimeProvider.System);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(passwords);
        builder.Services.AddSingleton(principals);
        builder.Services.AddSingleton(new LoginSessionService(db, Options.Create(new LoginSessionOptions()), TimeProvider.System));
        builder.Services.AddSingleton(services.GetRequiredService<TenantService>());
        builder.Services.AddSingleton(services.GetRequiredService<IAccessTokenService>());
        builder.Services.AddScoped<TenantContext.TenantContext>();
        builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext.TenantContext>());
        builder.Services.AddAuthentication(IdentityCookieConfiguration.ConfigureAuthentication)
            .AddCookie(options => IdentityCookieConfiguration.ConfigureCookie(options))
            .AddJwtBearer();
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options => options.AddPolicy("password-auth", _ =>
            System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("http-tests")));
        builder.Services.AddControllers().AddApplicationPart(typeof(IdentityTenant.Api.Controllers.PasswordAuthController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseMiddleware<IdentityTenant.Api.Middleware.TenantContextMiddleware>();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        var email = $"http-{Guid.NewGuid():N}@company.com";
        const string password = "http-test-password-2026";
        using var registration = await client.PostAsJsonAsync("/api/auth/password/register", new { email, name = "HTTP Test" });
        Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        var token = Assert.Single(mail.Tokens);
        using var verified = await client.PostAsJsonAsync("/api/auth/password/verify", new { token, password });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var replay = await client.PostAsJsonAsync("/api/auth/password/verify", new { token, password });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var incorrect = await client.PostAsJsonAsync("/api/auth/password/login", new { email, password = "incorrect-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, incorrect.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/password/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var access = await client.PostAsync("/api/auth/token", null);
        Assert.Equal(HttpStatusCode.OK, access.StatusCode);
        var body = await access.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using var revoked = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    private sealed class RecordingMail : IVerificationEmailSender
    {
        public bool IsConfigured => true;
        public List<string> Tokens { get; } = [];
        public Task SendAsync(string email, string token, CancellationToken ct)
        {
            Tokens.Add(token);
            return Task.CompletedTask;
        }
    }
}

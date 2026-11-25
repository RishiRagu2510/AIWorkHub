using System.Security.Claims;
using System.Text.Json;
using Google.Apis.Auth;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Controllers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenantContext;

namespace Backend.Authorization.Tests;

public sealed class GoogleSignInTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    [Theory]
    [InlineData("valid", 204)]
    
    
    [InlineData("missing-csrf", 400)]
    [InlineData("tampered-csrf", 400)]
    [InlineData("invalid-nonce", 401)]
    [InlineData("invalid-token", 401)]
    [InlineData("wrong-company", 401)]
    public async Task CredentialExchangeRequiresBrowserProtectionAndCompanyAuthorization(string scenario, int status)
    {
        using var scope = fixture.App.Services.CreateScope();
        var auth = new CaptureAuthentication();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAntiforgery(o => o.HeaderName = "X-Google-CSRF");
        services.AddSingleton<IAuthenticationService>(auth);
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Scheme = "https";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Authentication:Google:ClientId"] = "test-client" }).Build();
        var validator = new TestValidator(scenario);
        var controller = new GoogleSignInController(config, provider.GetRequiredService<IAntiforgery>(),
            provider.GetRequiredService<IDataProtectionProvider>(), validator,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleSignInController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        var configuration = Assert.IsType<OkObjectResult>(controller.Configuration());
        var body = JsonSerializer.SerializeToElement(configuration.Value);
        Assert.Equal("test-client", body.GetProperty("clientId").GetString());
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
        validator.ExpectedNonce = body.GetProperty("nonce").GetString()!;
        var cookies = context.Response.Headers.SetCookie.Select(c => c!.Split(';')[0]).ToArray();
        context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Scheme = "https";
        context.Request.Method = "POST";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        context.Request.Headers.Cookie = string.Join("; ", cookies);
        if (scenario == "invalid-nonce") context.Request.Headers.Cookie = PortalCookieManager.Key(context, "AIWorkHub.GoogleNonce") + "=invalid; " + string.Join("; ", cookies.Where(c => !c.StartsWith("AIWorkHub.GoogleNonce.")));
        if (scenario == "tampered-csrf") context.Request.Headers["X-Google-CSRF"] = "not-the-issued-token";
        else if (scenario != "missing-csrf") context.Request.Headers["X-Google-CSRF"] = body.GetProperty("csrfToken").GetString();
        var result = await controller.SignIn(new("test-credential"), scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>(), default);
        var actual = result is StatusCodeResult code ? code.StatusCode : ((ObjectResult)result).StatusCode;
        Assert.Equal(status, actual);
        if (status == 204)
        {
            Assert.NotNull(auth.Principal);
            Assert.Equal("101", auth.Principal.FindFirstValue(TenantClaimTypes.UserId));
            Assert.Equal("1", auth.Principal.FindFirstValue(TenantClaimTypes.TenantId));
            Assert.Equal("DEVELOPER", auth.Principal.FindFirstValue(TenantClaimTypes.Role));
        }
        else Assert.Null(auth.Principal);
        if (scenario is "invalid-nonce" or "missing-csrf" or "tampered-csrf") Assert.Equal(0, validator.Calls);
    }

    [Fact]
    public async Task RealValidatorRejectsMalformedCredentials()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => new GoogleCredentialValidator().ValidateAsync("not-a-google-token", "test-client", "nonce"));
    }

    private sealed class TestValidator(string scenario) : IGoogleCredentialValidator
    {
        public string ExpectedNonce = "";
        public int Calls;
        public Task<ClaimsPrincipal> ValidateAsync(string credential, string clientId, string nonce)
        {
            Calls++;
            Assert.Equal(ExpectedNonce, nonce);
            Assert.Equal("test-client", clientId);
            if (scenario == "invalid-token") throw new InvalidJwtException("Invalid signature");
            return Task.FromResult(SecurityFixture.Google(scenario == "wrong-company" ? "person@gmail.com" : "user101@company.com"));
        }
    }
    private sealed class CaptureAuthentication : IAuthenticationService
    {
        public ClaimsPrincipal? Principal;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) { Principal = principal; return Task.CompletedTask; }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}

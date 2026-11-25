using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TenantContext;

namespace IdentityTenant.Api.Authentication;

public static class IdentityCookieConfiguration
{
    public static void ConfigureAuthentication(AuthenticationOptions options)
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;


        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    }

    public static void ConfigureCookie(CookieAuthenticationOptions options, string? cookieDomain = null)
    {
        options.Cookie.Name = "AIWorkHub.Session";
        options.CookieManager = new PortalCookieManager();
        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        var domain = cookieDomain ?? Environment.GetEnvironmentVariable("COOKIE_DOMAIN");
        if (!string.IsNullOrWhiteSpace(domain))
        {
            options.Cookie.Domain = domain;
            options.Cookie.SameSite = SameSiteMode.Lax;
        }
        options.Cookie.Extensions.Add("Partitioned");

        options.SlidingExpiration = false;
        options.Events.OnSigningIn = async context =>
        {
            var sessions = context.HttpContext.RequestServices.GetRequiredService<LoginSessionService>();
            await sessions.RevokeAsync(context.HttpContext.User, context.HttpContext.RequestAborted);
            var expires = await sessions.CreateAsync(context.Principal!, context.HttpContext.RequestAborted);
            context.Properties.IsPersistent = true;
            context.Properties.AllowRefresh = false;
            context.Properties.ExpiresUtc = expires;
        };
        options.Events.OnSigningOut = context => context.HttpContext.RequestServices
            .GetRequiredService<LoginSessionService>()
            .RevokeAsync(context.HttpContext.User, context.HttpContext.RequestAborted);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var service = context.HttpContext.RequestServices.GetRequiredService<CompanyPrincipalService>();
            var sessions = context.HttpContext.RequestServices.GetRequiredService<LoginSessionService>();
            var expires = context.Principal is null
                ? null
                : await sessions.ValidateAsync(context.Principal, context.HttpContext.RequestAborted);
            var principal = expires is null
                ? null
                : await service.RefreshAsync(context.Principal!, context.HttpContext.RequestAborted);
            if (principal is null)
            {
                if (context.Principal is not null)
                    await sessions.RevokeAsync(context.Principal, context.HttpContext.RequestAborted);
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(LoginSessionService.SessionClaim,
                context.Principal!.FindFirstValue(LoginSessionService.SessionClaim)!));

            context.ShouldRenew = context.Principal!.HasClaim(c => c.Type == TenantClaimTypes.Permission);
            context.ReplacePrincipal(principal);
        };
    }
}
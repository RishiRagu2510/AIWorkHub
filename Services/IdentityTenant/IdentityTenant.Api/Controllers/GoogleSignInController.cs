using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Google.Apis.Auth;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Application.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/auth/google")]
public sealed class GoogleSignInController(
    IConfiguration configuration,
    IAntiforgery antiforgery,
    IDataProtectionProvider protection,
    IGoogleCredentialValidator validator,
    ILogger<GoogleSignInController> logger) : ControllerBase
{
    private readonly ITimeLimitedDataProtector protector = protection
        .CreateProtector("AIWorkHub.GoogleSignIn.v1").ToTimeLimitedDataProtector();

    private string NonceCookie => PortalCookieManager.Key(HttpContext, "AIWorkHub.GoogleNonce");

    private CookieOptions CookieOptions()
    {
        var cookieDomain = configuration["Authentication:CookieDomain"]
            ?? Environment.GetEnvironmentVariable("COOKIE_DOMAIN");
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = string.IsNullOrWhiteSpace(cookieDomain) ? SameSiteMode.None : SameSiteMode.Lax,
            Domain = string.IsNullOrWhiteSpace(cookieDomain) ? null : cookieDomain,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(10),
            IsEssential = true
        };
        options.Extensions.Add("Partitioned");
        return options;
    }

    [HttpGet("configuration")]
    public IActionResult Configuration()
    {
        Response.Headers.CacheControl = "no-store";
        var clientId = configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) return StatusCode(503);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Response.Cookies.Append(NonceCookie, protector.Protect(nonce, TimeSpan.FromMinutes(10)), CookieOptions());
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { clientId, nonce, csrfToken = tokens.RequestToken });
    }

    [HttpPost]
    public async Task<IActionResult> SignIn(CredentialRequest request,
        [FromServices] CompanyPrincipalService principals, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning("Google sign-in rejected: anti-forgery check failed ({Message})", ex.Message);
            return BadRequest(new { code = "GOOGLE_SIGN_IN_FAILED" });
        }

        var nonce = request.Nonce ?? "";
        if (string.IsNullOrWhiteSpace(nonce))
            try
            {
                nonce = protector.Unprotect(Request.Cookies[NonceCookie] ?? "");
            }
            catch (CryptographicException)
            {
                return Unauthorized(new { code = "GOOGLE_SIGN_IN_FAILED" });
            }

        try
        {
            var google = await validator.ValidateAsync(request.Credential,
                configuration["Authentication:Google:ClientId"]!, nonce);
            var principal = await principals.SignInGoogleAsync(google, ct);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            Response.Cookies.Delete(NonceCookie, CookieOptions());
            return NoContent();
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("Google validation failed: {Message}", ex.Message);
            return Unauthorized(new { code = "INVALID_GOOGLE_IDENTITY" });
        }
        catch (ArgumentException)
        {
            return Unauthorized(new { code = "INVALID_GOOGLE_IDENTITY" });
        }
        catch (LoginRejectedException error)
        {
            return Unauthorized(new { code = error.Code });
        }
    }

    public sealed record CredentialRequest([Required] [MaxLength(16384)] string Credential, string? Nonce = null);
}

public interface IGoogleCredentialValidator
{
    Task<ClaimsPrincipal> ValidateAsync(string credential, string clientId, string nonce);
}

public sealed class GoogleCredentialValidator : IGoogleCredentialValidator
{
    public async Task<ClaimsPrincipal> ValidateAsync(string credential, string clientId, string nonce)
    {
        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidJwtException("Missing audience.");

        var payload = await GoogleJsonWebSignature.ValidateAsync(credential,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId, "68960392476-4gmjejdqplg3165tq3d9u7oud64dk0eb.apps.googleusercontent.com" }
            });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(credential);
        var nonces = token.Claims.Where(c => c.Type == "nonce").ToArray();
        if (nonces.Length != 1 || nonces[0].Value != nonce)
            throw new InvalidJwtException("Invalid sign-in nonce.");
        var identity = new ClaimsIdentity("Google");
        identity.AddClaim(new Claim("sub", payload.Subject ?? ""));
        identity.AddClaim(new Claim("email", payload.Email ?? ""));
        identity.AddClaim(new Claim("name", payload.Name ?? "Google user"));
        identity.AddClaim(new Claim("email_verified", payload.EmailVerified ? "true" : "false"));
        return new ClaimsPrincipal(identity);
    }
}
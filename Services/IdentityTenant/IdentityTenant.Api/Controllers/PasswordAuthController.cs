using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/auth/password")]
[EnableRateLimiting("password-auth")]
public sealed class PasswordAuthController(PasswordLoginService service) : ControllerBase
{
    [HttpPost("register")]
    [RequireSessionOrigin]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            await service.RegisterAsync(request.Email, request.Name, ct);
        }
        catch (Exception e) when (e is SmtpException or InvalidOperationException)
        {
            return StatusCode(503, new { code = "EMAIL_UNAVAILABLE" });
        }

        return Accepted(new
        {
            message =
                "If your email is eligible, a verification link will arrive shortly. If you already set a password, sign in instead."
        });
    }

    [HttpPost("verify")]
    [RequireSessionOrigin]
    public async Task<IActionResult> Verify(VerifyRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            await service.VerifyAsync(request.Token, request.Password, ct);
            return NoContent();
        }
        catch (LoginRejectedException)
        {
            return BadRequest(new { code = "INVALID_VERIFICATION" });
        }
    }

    [HttpPost("login")]
    [RequireSessionOrigin]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var principal = await service.LoginAsync(request.Email, request.Password, ct);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return NoContent();
        }
        catch (LoginRejectedException error) when (error.Code == "ROLE_ASSIGNMENT_PENDING")
        {
            return Unauthorized(new { code = error.Code });
        }
        catch (LoginRejectedException)
        {
            return Unauthorized(new { code = "INVALID_CREDENTIALS" });
        }
    }

    public sealed record RegisterRequest(
        [Required]
        [EmailAddress]
        [StringLength(320)]
        string Email,
        [Required] [StringLength(200)] string Name);

    public sealed record VerifyRequest(
        [Required]
        [StringLength(64, MinimumLength = 64)]
        string Token,
        [Required]
        [StringLength(128, MinimumLength = 12)]
        string Password);

    public sealed record LoginRequest(
        [Required]
        [EmailAddress]
        [StringLength(320)]
        string Email,
        [Required] [StringLength(128)] string Password);
}
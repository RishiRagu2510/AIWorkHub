using System.Security.Claims;
using Authentication;
using Authorization;
using Google.Apis.Auth.AspNetCore3;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController : ControllerBase
{
    private readonly IAccessTokenService _accessTokenService;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ITenantContext _tenantContext;

    public AuthController(
        ITenantContext tenantContext,
        IAccessTokenService accessTokenService,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _tenantContext = tenantContext;
        _accessTokenService = accessTokenService;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"];
        if (string.IsNullOrWhiteSpace(frontendBaseUrl) && _environment.IsDevelopment())
            frontendBaseUrl = "http://localhost:5173";

        if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var frontendUri) ||
            (frontendUri.Scheme != Uri.UriSchemeHttps &&
             !(_environment.IsDevelopment() && frontendUri.Scheme == Uri.UriSchemeHttp)))
            return Problem("Frontend sign-in redirect is not configured.", statusCode: 503);


        var requestOrigin = $"{Request.Scheme}://{Request.Host}";
        var portalOrigins = _configuration.GetSection("Frontend:PortalOrigins").Get<string[]>() ?? [];
        var localPortal = _environment.IsDevelopment() && Request.Scheme == "http" &&
                          Request.Host.Host is "localhost" or "127.0.0.1" &&
                          Request.Host.Port is 5171 or 5173 or 5174 or 5175;
        if (localPortal || portalOrigins.Any(origin =>
                string.Equals(origin.TrimEnd('/'), requestOrigin, StringComparison.OrdinalIgnoreCase)))
            frontendUri = new Uri(requestOrigin);


        var properties = new AuthenticationProperties
        {
            RedirectUri = new Uri(frontendUri, "/auth/callback").AbsoluteUri
        };

        return Challenge(
            properties,
            GoogleOpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me([FromServices] TenantService tenantService, CancellationToken cancellationToken)
    {
        if (!(User.Identity?.IsAuthenticated ?? false)) return Unauthorized();

        var userId = User.FindFirstValue(
            TenantClaimTypes.UserId);

        var tenantId = User.FindFirstValue(
            TenantClaimTypes.TenantId);

        var role = User.FindFirstValue(
            TenantClaimTypes.Role);

        if (!long.TryParse(tenantId, out var currentTenantId))
            return Unauthorized();
        var tenant = await tenantService.GetByIdAsync(currentTenantId, cancellationToken);

        return Ok(new
        {
            isAuthenticated = true,
            user = new
            {
                id = userId,
                tenantId,
                tenantName = tenant?.Name,
                role,
                roles = User.FindAll(TenantClaimTypes.Role).Select(c => c.Value).Distinct().Order().ToArray(),
                permissions = User.FindAll(TenantClaimTypes.Permission).Select(c => c.Value).Distinct().Order()
                    .ToArray(),
                allowedPortals =
                    User.FindAll(TenantClaimTypes.Portal).Select(c => c.Value).Distinct().Order().ToArray(),
                allowedFeatures = User.FindAll(TenantClaimTypes.Feature).Select(c => c.Value).Distinct().Order()
                    .ToArray(),
                email = User.FindFirstValue(
                    ClaimTypes.Email),
                name = User.FindFirstValue("name")
            }
        });
    }

    [Authorize]
    [HttpGet("token-dev")]
    public IActionResult CreateDevelopmentToken()
    {
        if (!_environment.IsDevelopment()) return NotFound();

        if (!_tenantContext.IsAuthenticated) return Unauthorized();

        var accessToken =
            _accessTokenService.CreateAccessToken(User);

        return Ok(new
        {
            accessToken,
            tokenType = "Bearer"
        });
    }

    [RequirePermission("ISSUE_CREATE")]
    [HttpGet("permission-test")]
    public IActionResult PermissionTest()
    {
        return Ok(new
        {
            authorized = true,
            permission = "ISSUE_CREATE",
            message =
                "The current user has ISSUE_CREATE permission."
        });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("context")]
    public IActionResult Context()
    {
        var permissions =
            User.FindAll(TenantClaimTypes.Permission)
                .Select(claim => claim.Value)
                .Distinct()
                .OrderBy(permission => permission)
                .ToList();

        return Ok(new
        {
            isAuthenticated =
                _tenantContext.IsAuthenticated,

            userId =
                _tenantContext.UserId,

            tenantId =
                _tenantContext.TenantId,

            roles =
                _tenantContext.Roles,

            permissionCount =
                permissions.Count,

            permissions
        });
    }

    [Authorize]
    [HttpPost("token")]
    [RequireSessionOrigin]
    public IActionResult CreateToken()
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();

        var accessToken =
            _accessTokenService.CreateAccessToken(User);

        return Ok(new
        {
            accessToken,
            tokenType = "Bearer"
        });
    }

    [HttpPost("logout")]
    [RequireSessionOrigin]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();

        return Ok(new
        {
            message = "Logged out successfully."
        });
    }
}
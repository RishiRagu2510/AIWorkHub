using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Authentication;

public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly JwtSettings _settings;

    public JwtAccessTokenService(
        IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        ValidateSettings();
    }
    public string CreateAccessToken(
        ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            principal.FindAll(AiWorkHubClaimTypes.UserId).Count() != 1 ||
            principal.FindAll(AiWorkHubClaimTypes.TenantId).Count() != 1)
            throw new InvalidOperationException("A provisioned authenticated security context is required.");

        var userId =
            principal.FindFirstValue(
                AiWorkHubClaimTypes.UserId);

        var tenantId =
            principal.FindFirstValue(
                AiWorkHubClaimTypes.TenantId);

        if (!long.TryParse(userId, out var parsedUserId) || parsedUserId <= 0)
        {
            throw new InvalidOperationException(
                "Authenticated principal does not contain an AIWorkHub user ID.");
        }

        if (!long.TryParse(tenantId, out var parsedTenantId) || parsedTenantId <= 0)
        {
            throw new InvalidOperationException(
                "Authenticated principal does not contain an AIWorkHub tenant ID.");
        }

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                userId),

            new(
                AiWorkHubClaimTypes.UserId,
                userId),

            new(
                AiWorkHubClaimTypes.TenantId,
                tenantId)
        };

        var email =
            principal.FindFirstValue(
                ClaimTypes.Email);

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(
                new Claim(
                    JwtRegisteredClaimNames.Email,
                    email));
        }

        var name =
            principal.FindFirstValue("name")
            ?? principal.FindFirstValue(
                ClaimTypes.Name);

        if (!string.IsNullOrWhiteSpace(name))
        {
            claims.Add(
                new Claim(
                    JwtRegisteredClaimNames.Name,
                    name));
        }

        var roles =
            principal
                .FindAll(AiWorkHubClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            claims.Add(
                new Claim(
                    AiWorkHubClaimTypes.Role,
                    role));
        }

        var permissions =
            principal
                .FindAll(AiWorkHubClaimTypes.Permission)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in permissions)
        {
            claims.Add(
                new Claim(
                    AiWorkHubClaimTypes.Permission,
                    permission));
        }

        var portals =
            principal
                .FindAll(AiWorkHubClaimTypes.Portal)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var portal in portals)
        {
            claims.Add(
                new Claim(
                    AiWorkHubClaimTypes.Portal,
                    portal));
        }

        var features =
            principal
                .FindAll(AiWorkHubClaimTypes.Feature)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in features)
        {
            claims.Add(
                new Claim(
                    AiWorkHubClaimTypes.Feature,
                    feature));
        }

        var securityKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _settings.SigningKey))
            {
                KeyId = JwtSettings.KeyId
            };

        var signingCredentials =
            new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

        var issuedAt =
            DateTime.UtcNow;

        var expiresAt =
            issuedAt.AddMinutes(
                _settings.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.Issuer))
        {
            throw new InvalidOperationException(
                "JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.Audience))
        {
            throw new InvalidOperationException(
                "JWT audience is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.SigningKey))
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured.");
        }

        if (_settings.ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiration must be greater than zero.");
        }
    }
}
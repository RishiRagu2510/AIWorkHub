using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityTenant.Api.Configuration;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TenantContext;

namespace IdentityTenant.Api.Authentication;

public sealed class LoginSessionService(
    IdentityTenantDbContext database,
    IOptions<LoginSessionOptions> options,
    TimeProvider clock)
{
    public const string SessionClaim = "aiworkhub:session";

    public async Task<DateTimeOffset> CreateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated ||
            principal.FindAll(TenantClaimTypes.UserId).Count() != 1 ||
            !long.TryParse(principal.FindFirstValue(TenantClaimTypes.UserId), out var userId) || userId <= 0)
            throw new InvalidOperationException("A provisioned identity is required to create a session.");

        var handle = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = clock.GetUtcNow().AddDays(options.Value.AbsoluteDays);
        database.UserSessions.Add(new UserSession(userId, Hash(handle), expires,
            principal.FindFirstValue("aiworkhub:login_method") ?? "GOOGLE"));
        await database.SaveChangesAsync(ct);
        foreach (var claim in identity.FindAll(SessionClaim).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(SessionClaim, handle));
        return expires;
    }

    public async Task<DateTimeOffset?> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!TryRead(principal, out var hash, out var userId)) return null;
        var now = clock.GetUtcNow();
        var idleCutoff = now.AddHours(-options.Value.IdleHours);
        var active = database.UserSessions.Where(s => s.SessionTokenHash == hash && s.UserId == userId &&
                                                      s.RevokedAt == null && s.ExpiresAt > now &&
                                                      (s.LastSeenAt ?? s.CreatedAt) > idleCutoff);

        if (await active.ExecuteUpdateAsync(update => update
                .SetProperty(s => s.LastSeenAt, now).SetProperty(s => s.UpdatedAt, now), ct) != 1)
            return null;
        return await active.Select(s => (DateTimeOffset?)s.ExpiresAt).SingleOrDefaultAsync(ct);
    }

    public async Task RevokeAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!TryRead(principal, out var hash, out var userId)) return;
        var now = clock.GetUtcNow();
        await database.UserSessions.Where(s => s.SessionTokenHash == hash && s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevokedAt, now)
                .SetProperty(s => s.UpdatedAt, now), ct);
    }

    private static bool TryRead(ClaimsPrincipal principal, out string hash, out long userId)
    {
        hash = "";
        userId = 0;
        var handle = principal.FindFirstValue(SessionClaim);
        if (principal.FindAll(SessionClaim).Count() != 1 || handle?.Length != 64 ||
            principal.FindAll(TenantClaimTypes.UserId).Count() != 1 ||
            !long.TryParse(principal.FindFirstValue(TenantClaimTypes.UserId), out userId) || userId <= 0)
            return false;
        hash = Hash(handle);
        return true;
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
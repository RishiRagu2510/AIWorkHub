using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentityTenant.Api.Authentication;

public sealed class PasswordLoginService(
    IdentityTenantDbContext db,
    IMembershipRepository memberships,
    ITenantRepository tenants,
    CompanyPrincipalService principals,
    IPasswordHasher<LocalCredential> hasher,
    IVerificationEmailSender mail,
    TimeProvider clock)
{
    private static readonly LocalCredential Dummy = new();

    private static readonly string DummyHash = new PasswordHasher<LocalCredential>(
            Options.Create(new PasswordHasherOptions { IterationCount = 210000 }))
        .HashPassword(Dummy, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    private static string Normalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static LoginRejectedException Invalid()
    {
        return new LoginRejectedException("INVALID_CREDENTIALS", "Email or password is incorrect.");
    }

    private static LoginRejectedException InvalidLink()
    {
        return new LoginRejectedException("INVALID_VERIFICATION", "The verification link is invalid or expired.");
    }

    private async Task<(Tenant tenant, User? user, TenantInvitation? invitation)?> Eligible(string email,
        CancellationToken ct)
    {
        var users = await memberships.FindUsersAsync(email, ct);
        if (users.Count > 1) return null;
        var user = users.SingleOrDefault();
        TenantInvitation? invitation = null;
        Tenant? tenant;
        if (user is not null)
        {
            if (user.Status != UserStatus.Active) return null;
            tenant = await tenants.GetByIdAsync(user.TenantId, ct);
        }
        else
        {
            var invitations = await memberships.FindInvitationsAsync(email, ct);
            if (invitations.Count > 1) return null;
            invitation = invitations.SingleOrDefault();
            if (invitation is not null && invitation.RoleCode != "DEVELOPER") return null;
            tenant = invitation is not null
                ? await tenants.GetByIdAsync(invitation.TenantId, ct)
                : await tenants.FindByEmailDomainAsync(new MailAddress(email).Host, ct);
        }

        return tenant?.Status == TenantStatus.Active ? (tenant, user, invitation) : null;
    }

    public async Task RegisterAsync(string email, string name, CancellationToken ct)
    {
        if (!mail.IsConfigured) throw new InvalidOperationException("Email delivery unavailable.");
        email = Normalize(email);
        string? token = null;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await memberships.LockAsync(ct);
            var eligible = await Eligible(email, ct);
            if (eligible is null) return;
            if (eligible.Value.user is { } user &&
                await db.LocalCredentials.AnyAsync(c => c.UserId == user.Id, ct)) return;
            var now = clock.GetUtcNow();
            if (await db.EmailVerifications.AnyAsync(v => v.Email == email && v.CreatedAt > now.AddMinutes(-1), ct))
                return;
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            db.EmailVerifications.Add(new EmailVerification
            {
                TokenHash = Hash(token), Email = email,
                DisplayName = name.Trim(), TenantId = eligible.Value.tenant.Id, CreatedAt = now,
                ExpiresAt = now.AddMinutes(30)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        try
        {
            await mail.SendAsync(email, token, ct);
        }
        catch
        {
            var hash = Hash(token);
            await db.EmailVerifications.Where(v => v.TokenHash == hash && v.ConsumedAt == null)
                .ExecuteDeleteAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task VerifyAsync(string token, string password, CancellationToken ct)
    {
        if (token.Length != 64 || password.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(password))
            throw InvalidLink();
        var hash = Hash(token);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await memberships.LockAsync(ct);
        var now = clock.GetUtcNow();
        var pending =
            await db.EmailVerifications.SingleOrDefaultAsync(
                v => v.TokenHash == hash && v.ConsumedAt == null && v.ExpiresAt > now, ct);
        if (pending is null) throw InvalidLink();
        var eligible = await Eligible(pending.Email, ct);
        if (eligible is null || eligible.Value.tenant.Id != pending.TenantId) throw InvalidLink();
        var user = eligible.Value.user;
        if (user is null)
        {
            user = new User(pending.TenantId, pending.Email, pending.DisplayName);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            if (eligible.Value.invitation is { } invitation) invitation.AcceptedAt = now;
        }

        if (await db.LocalCredentials.AnyAsync(c => c.UserId == user.Id, ct)) throw InvalidLink();
        var credential = new LocalCredential { UserId = user.Id, CreatedAt = now, UpdatedAt = now };
        credential.PasswordHash = hasher.HashPassword(credential, password);
        db.LocalCredentials.Add(credential);
        user.VerifyEmail();
        await db.EmailVerifications.Where(v =>
                v.Email == pending.Email && v.TenantId == pending.TenantId && v.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.ConsumedAt, now), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<ClaimsPrincipal> LoginAsync(string email, string password, CancellationToken ct)
    {
        email = Normalize(email);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await memberships.LockAsync(ct);
        var eligible = await Eligible(email, ct);
        var user = eligible?.user;
        var credential = user is null
            ? null
            : await db.LocalCredentials.SingleOrDefaultAsync(c => c.UserId == user.Id, ct);
        if (credential is null || user is null || !user.EmailVerified)
        {
            hasher.VerifyHashedPassword(Dummy, DummyHash, password);
            throw Invalid();
        }

        var now = clock.GetUtcNow();
        if (credential.LockedUntil > now)
        {
            hasher.VerifyHashedPassword(Dummy, DummyHash, password);
            throw Invalid();
        }

        var verified = hasher.VerifyHashedPassword(credential, credential.PasswordHash, password);
        if (verified == PasswordVerificationResult.Failed)
        {
            credential.FailedAttempts++;
            if (credential.FailedAttempts >= 5)
            {
                credential.LockedUntil = now.AddMinutes(15);
                credential.FailedAttempts = 0;
            }

            credential.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw Invalid();
        }

        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
            credential.PasswordHash = hasher.HashPassword(credential, password);
        var principal = await principals.SignInPasswordAsync(user.Id, user.TenantId, ct);
        credential.FailedAttempts = 0;
        credential.LockedUntil = null;
        credential.UpdatedAt = now;
        user.RecordLogin();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return principal;
    }
}
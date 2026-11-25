using System.Net.Mail;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Contracts;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Api.Services.Invitations;

public enum InvitationOutcome
{
    Ok,
    NotAdmin,
    Invalid,
    NotAvailable,
    NotFound,
    AlreadyAccepted,
    InvalidPage
}

public sealed record InvitationResult(InvitationOutcome Outcome, InvitationDto? Invitation = null, string? Code = null);

public sealed record InvitationListResult(InvitationOutcome Outcome, InvitationPageDto? Page = null);

public sealed class InvitationService(IdentityTenantDbContext db, IMembershipRepository memberships)
{
    private const int PageSize = 50;

    private Task<bool> ActiveAdmin(long tenant, long user, CancellationToken ct)
    {
        return db.Users.AnyAsync(u => u.Id == user && u.TenantId == tenant && u.Status == UserStatus.Active
                                      && db.Tenants.Any(t => t.Id == tenant && t.Status == TenantStatus.Active)
                                      && db.UserRoles.Any(ur =>
                                          ur.TenantId == tenant && ur.UserId == user && db.Roles.Any(r =>
                                              r.Id == ur.RoleId && r.TenantId == tenant && r.Code == "ADMIN")), ct);
    }

    private static InvitationDto ToDto(TenantInvitation i)
    {
        return new InvitationDto(i.Id, i.TenantId, i.Email, i.RoleCode, i.InvitedByUserId, i.CreatedAt, i.ExpiresAt,
            i.AcceptedAt,
            i.RevokedAt);
    }

    public async Task<InvitationListResult> List(long tenant, long user, int page, CancellationToken ct)
    {
        if (!await ActiveAdmin(tenant, user, ct)) return new InvitationListResult(InvitationOutcome.NotAdmin);
        if (page < 1 || page > 100000) return new InvitationListResult(InvitationOutcome.InvalidPage);
        var query = db.Set<TenantInvitation>().AsNoTracking().Where(i => i.TenantId == tenant);
        var items = await query.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id).Skip((page - 1) * PageSize)
            .Take(PageSize).ToListAsync(ct);
        return new InvitationListResult(InvitationOutcome.Ok,
            new InvitationPageDto(items.Select(ToDto).ToList(), page, PageSize, await query.CountAsync(ct)));
    }

    public async Task<InvitationResult> Create(long tenant, long user, InviteRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email || email.Length > 320 ||
            email.Count(c => c == '@') != 1 || request.ExpiresInDays is < 1 or > 30)
            return new InvitationResult(InvitationOutcome.Invalid, Code: "INVALID_INVITATION");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await memberships.LockAsync(ct);
        if (!await ActiveAdmin(tenant, user, ct)) return new InvitationResult(InvitationOutcome.NotAdmin);
        if (!await db.IdentityProviders.AnyAsync(
                p => p.TenantId == tenant && p.ProviderType == IdentityProviderType.Google && p.Enabled, ct) ||
            !await db.Roles.AnyAsync(r => r.TenantId == tenant && r.Code == "DEVELOPER", ct))
            return new InvitationResult(InvitationOutcome.NotAvailable, Code: "INVITATION_NOT_AVAILABLE");

        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email, ct))
            return new InvitationResult(InvitationOutcome.NotAvailable, Code: "INVITATION_NOT_AVAILABLE");
        var now = DateTimeOffset.UtcNow;
        await db.Set<TenantInvitation>()
            .Where(i => i.Email == email && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RevokedAt, now), ct);
        if (await db.Set<TenantInvitation>()
                .AnyAsync(i => i.Email == email && i.AcceptedAt == null && i.RevokedAt == null, ct))
            return new InvitationResult(InvitationOutcome.NotAvailable, Code: "INVITATION_NOT_AVAILABLE");
        var invitation = new TenantInvitation
        {
            TenantId = tenant, Email = email!, InvitedByUserId = user, ExpiresAt = now.AddDays(request.ExpiresInDays)
        };
        db.Add(invitation);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new InvitationResult(InvitationOutcome.Ok, ToDto(invitation));
    }

    public async Task<InvitationResult> Revoke(long tenant, long user, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await memberships.LockAsync(ct);
        if (!await ActiveAdmin(tenant, user, ct)) return new InvitationResult(InvitationOutcome.NotAdmin);
        var invitation = await db.Set<TenantInvitation>()
            .SingleOrDefaultAsync(i => i.Id == id && i.TenantId == tenant, ct);
        if (invitation is null) return new InvitationResult(InvitationOutcome.NotFound);
        if (invitation.AcceptedAt is not null)
            return new InvitationResult(InvitationOutcome.AlreadyAccepted, Code: "INVITATION_ALREADY_ACCEPTED");
        invitation.RevokedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new InvitationResult(InvitationOutcome.Ok);
    }
}
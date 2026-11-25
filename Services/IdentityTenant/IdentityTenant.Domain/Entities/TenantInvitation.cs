namespace IdentityTenant.Domain.Entities;

public sealed class TenantInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TenantId { get; set; }
    public string Email { get; set; } = "";
    public string RoleCode { get; set; } = "DEVELOPER";
    public long InvitedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
using IdentityTenant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace IdentityTenant.Infrastructure.Persistence.Configurations;
public sealed class TenantInvitationConfiguration:IEntityTypeConfiguration<TenantInvitation>
{
 public void Configure(EntityTypeBuilder<TenantInvitation> b){
  b.ToTable("tenant_invitations");b.HasKey(i=>i.Id);
  b.Property(i=>i.Id).HasColumnName("id");b.Property(i=>i.TenantId).HasColumnName("tenant_id");
  b.Property(i=>i.Email).HasColumnName("email").HasMaxLength(320);b.Property(i=>i.RoleCode).HasColumnName("role_code").HasMaxLength(50);
  b.Property(i=>i.InvitedByUserId).HasColumnName("invited_by_user_id");b.Property(i=>i.CreatedAt).HasColumnName("created_at");
  b.Property(i=>i.ExpiresAt).HasColumnName("expires_at");b.Property(i=>i.AcceptedAt).HasColumnName("accepted_at");b.Property(i=>i.RevokedAt).HasColumnName("revoked_at");
  b.HasIndex(i=>i.Email).IsUnique().HasFilter("accepted_at IS NULL AND revoked_at IS NULL");
  b.HasOne<Tenant>().WithMany().HasForeignKey(i=>i.TenantId).OnDelete(DeleteBehavior.Restrict);
 }
}

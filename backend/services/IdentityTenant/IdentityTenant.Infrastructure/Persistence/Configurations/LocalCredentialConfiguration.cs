using IdentityTenant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace IdentityTenant.Infrastructure.Persistence.Configurations;
public sealed class LocalCredentialConfiguration : IEntityTypeConfiguration<LocalCredential>
{
    public void Configure(EntityTypeBuilder<LocalCredential> b)
    {
        b.ToTable("local_credentials"); b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512);
        b.Property(x => x.FailedAttempts).HasColumnName("failed_attempts");
        b.Property(x => x.LockedUntil).HasColumnName("locked_until");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<User>().WithOne().HasForeignKey<LocalCredential>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class EmailVerificationConfiguration : IEntityTypeConfiguration<EmailVerification>
{
    public void Configure(EntityTypeBuilder<EmailVerification> b)
    {
        b.ToTable("email_verifications"); b.HasKey(x => x.TokenHash);
        b.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
        b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        b.HasIndex(x => new {x.Email, x.CreatedAt});
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

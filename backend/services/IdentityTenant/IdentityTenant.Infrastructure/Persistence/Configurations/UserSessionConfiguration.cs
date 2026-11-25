using IdentityTenant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityTenant.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration :
    IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");
        builder.Property(x => x.LoginMethod).HasColumnName("login_method").HasMaxLength(20);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.SessionTokenHash)
            .HasColumnName("session_token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(x => x.LastSeenAt)
            .HasColumnName("last_seen_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.SessionTokenHash)
            .IsUnique()
            .HasDatabaseName("uq_user_sessions_token_hash");

        builder.HasIndex(x => x.UserId);

        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
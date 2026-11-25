using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityTenant.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration :
    IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<UserStatus>(value, true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.EmailVerified)
            .HasColumnName("email_verified")
            .IsRequired();

        builder.Property(x => x.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new
            {
                x.TenantId,
                x.Email
            })
            .IsUnique()
            .HasDatabaseName("uq_users_tenant_email");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
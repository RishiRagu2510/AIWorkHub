using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityTenant.Infrastructure.Persistence.Configurations;

public sealed class IdentityProviderConfiguration :
    IEntityTypeConfiguration<IdentityProvider>
{
    public void Configure(EntityTypeBuilder<IdentityProvider> builder)
    {
        builder.ToTable("identity_providers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(x => x.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ProviderType)
            .HasColumnName("provider_type")
            .HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<IdentityProviderType>(value, true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Authority)
            .HasColumnName("authority")
            .HasMaxLength(500);

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
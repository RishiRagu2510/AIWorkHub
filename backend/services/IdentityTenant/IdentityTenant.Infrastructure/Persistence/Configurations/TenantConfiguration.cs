using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityTenant.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration :
    IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique()
            .HasDatabaseName("uq_tenants_slug");

        builder.Property(x => x.EmailDomain)
            .HasColumnName("email_domain")
            .HasMaxLength(253);

        
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<TenantStatus>(value, true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
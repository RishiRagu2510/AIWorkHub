using IdentityTenant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityTenant.Infrastructure.Persistence.Configurations;

public sealed class UserExternalIdentityConfiguration :
    IEntityTypeConfiguration<UserExternalIdentity>
{
    public void Configure(
        EntityTypeBuilder<UserExternalIdentity> builder)
    {
        builder.ToTable("user_external_identities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.IdentityProviderId)
            .HasColumnName("identity_provider_id")
            .IsRequired();

        builder.Property(x => x.ExternalSubject)
            .HasColumnName("external_subject")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new
            {
                x.IdentityProviderId,
                x.ExternalSubject
            })
            .IsUnique()
            .HasDatabaseName("uq_external_identity");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<IdentityProvider>()
            .WithMany()
            .HasForeignKey(x => x.IdentityProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
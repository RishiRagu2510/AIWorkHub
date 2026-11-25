using Jira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Jira.Infrastructure.Persistence.Configurations;
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts"); b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(a => a.TenantId).HasColumnName("tenant_id");
        b.Property(a => a.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(a => a.Status).HasColumnName("status").HasMaxLength(30).HasDefaultValue("ACTIVE").IsRequired();
        b.Property(a => a.Version).IsRowVersion();
        b.Property(a => a.CreatedAt).HasColumnName("created_at");
        b.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        b.HasAlternateKey(a => new { a.TenantId, a.Id }).HasName("uq_accounts_tenant_id_id");
    }
}

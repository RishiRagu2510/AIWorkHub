using Jira.Domain.Entities;
using Jira.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jira.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration :
    IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.Property(x => x.Version).IsRowVersion();

        builder.ToTable("projects", table => {
            table.HasCheckConstraint("ck_projects_not_self_parent", "parent_project_id IS NULL OR parent_project_id <> id");
            table.HasCheckConstraint("ck_projects_type", "project_type IN ('SOFTWARE', 'SERVICE', 'BUSINESS', 'BANKING')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.HasOne<Account>().WithMany().HasForeignKey(x => new { x.TenantId, x.AccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.AccountId }).HasDatabaseName("ix_projects_tenant_account");
        builder.Property(x => x.ParentProjectId).HasColumnName("parent_project_id");
        builder.HasAlternateKey(x => new { x.TenantId, x.Id }).HasName("uq_projects_tenant_id_id");
        builder.HasOne<Project>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ParentProjectId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_projects_parent_same_tenant");
        builder.HasIndex(x => new { x.TenantId, x.ParentProjectId }).HasDatabaseName("ix_projects_tenant_parent");

        builder.Property(x => x.Key)
            .HasColumnName("key")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description");

        builder.Property(x => x.ProjectType)
            .HasColumnName("project_type")
            .HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<ProjectType>(
                    value,
                    true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<ProjectStatus>(
                    value,
                    true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ProjectLeadUserId)
            .HasColumnName("project_lead_user_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new
            {
                x.TenantId,
                x.Key
            })
            .IsUnique()
            .HasDatabaseName("uq_projects_tenant_key");

        
        
        

        builder.HasIndex(x => x.TenantId);
    }
}
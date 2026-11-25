using Jira.Domain.Entities;
using Jira.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jira.Infrastructure.Persistence.Configurations;

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.Property(x => x.Version).IsRowVersion();

        builder.ToTable("issues");
        builder.Property(x=>x.OriginalEstimateMinutes).HasColumnName("original_estimate_minutes");
        builder.Property(x=>x.RemainingEstimateMinutes).HasColumnName("remaining_estimate_minutes");
        builder.Property(x=>x.LoggedTimeMinutes).HasColumnName("logged_time_minutes");
        builder.Property(x=>x.BacklogOrder).HasColumnName("backlog_order");
        builder.HasMany(x=>x.Labels).WithOne().HasForeignKey(x=>x.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x=>x.Releases).WithOne().HasForeignKey(x=>x.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x=>x.Assignees).WithOne().HasForeignKey(x=>x.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x=>x.Labels).AutoInclude();
        builder.Navigation(x=>x.Releases).AutoInclude();
        builder.Navigation(x=>x.Assignees).AutoInclude();

        builder.HasKey(issue => issue.Id);

        builder.Property(issue => issue.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(issue => issue.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(issue => issue.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(issue => issue.IssueNumber)
            .HasColumnName("issue_number")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(issue => issue.IssueKey)
            .HasColumnName("issue_key")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(issue => issue.IssueKey)
            .IsUnique();

        builder.Property(issue => issue.IssueType)
            .HasColumnName("issue_type")
            .HasConversion(
                issueType => issueType.ToString().ToUpperInvariant(),
                value => Enum.Parse<IssueType>(
                    value,
                    ignoreCase: true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(issue => issue.Summary)
            .HasColumnName("summary")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(issue => issue.Description)
            .HasColumnName("description");

        builder.Property(issue => issue.StatusCode)
            .HasColumnName("status_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(issue => issue.Priority)
            .HasColumnName("priority")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(issue => issue.ReporterUserId)
            .HasColumnName("reporter_user_id")
            .IsRequired();

        builder.Property(issue => issue.AssigneeUserId)
            .HasColumnName("assignee_user_id");

        builder.Property(issue => issue.ParentIssueId)
            .HasColumnName("parent_issue_id");

        builder.Property(issue => issue.ComponentId)
            .HasColumnName("component_id");

        builder.Property(issue => issue.StoryPoints)
            .HasColumnName("story_points");

        builder.Property(issue => issue.DueDate)
            .HasColumnName("due_date");

        builder.Property(issue => issue.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(issue => issue.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(issue => issue.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(issue => issue.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        builder.Property(issue => issue.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(issue => issue.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(issue => new
        {
            issue.TenantId,
            issue.ProjectId,
            issue.IssueNumber
        })
        .IsUnique();

        builder.HasIndex(issue => new
        {
            issue.TenantId,
            issue.ProjectId
        });

        builder.HasIndex(issue => new
        {
            issue.TenantId,
            issue.IsDeleted
        });
    }
}
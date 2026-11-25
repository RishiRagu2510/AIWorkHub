using Jira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jira.Infrastructure.Persistence.Configurations;

public sealed class IssueCommentConfiguration
    : IEntityTypeConfiguration<IssueComment>
{
    public void Configure(
        EntityTypeBuilder<IssueComment> builder)
    {
        builder.ToTable("issue_comments");
        builder.Property(comment => comment.Version).IsRowVersion();

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(comment => comment.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(comment => comment.IssueId)
            .HasColumnName("issue_id")
            .IsRequired();

        builder.Property(comment => comment.AuthorUserId)
            .HasColumnName("author_user_id")
            .IsRequired();

        builder.Property(comment => comment.Body)
            .HasColumnName("body")
            .IsRequired();

        builder.Property(comment => comment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(comment => comment.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(comment => new
        {
            comment.TenantId,
            comment.IssueId
        });
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Jira.Infrastructure.Persistence;
public sealed class AttachmentRecord
{
    public Guid Id { get; set; }
    public long TenantId { get; set; }
    public long IssueId { get; set; }
    public long AuthorUserId { get; set; }
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class UserPreference
{
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "null";
    public uint Version { get; set; }
}
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<AttachmentRecord>
{
    public void Configure(EntityTypeBuilder<AttachmentRecord> b) {
        b.ToTable("issue_file_objects"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.IssueId).HasColumnName("issue_id"); b.Property(x => x.AuthorUserId).HasColumnName("author_user_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(255); b.Property(x => x.Size).HasColumnName("size");
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.HasIndex(x => new { x.TenantId, x.IssueId });
    }
}
public sealed class PreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> b) {
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.ToTable("user_workspace_preferences"); b.HasKey(x => new { x.TenantId, x.UserId, x.Key });
        b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Key).HasColumnName("key").HasMaxLength(160); b.Property(x => x.Value).HasColumnName("value").HasColumnType("jsonb");
        b.Property(x => x.Version).IsRowVersion();
    }
}

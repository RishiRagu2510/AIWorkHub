using Jira.Infrastructure.Persistence.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Jira.Infrastructure.Persistence.Configurations;
public sealed class CollaborationConfiguration : IEntityTypeConfiguration<ActivityEvent>, IEntityTypeConfiguration<IssueWatcher>,
    IEntityTypeConfiguration<WorkflowRules>, IEntityTypeConfiguration<RequestReplay>, IEntityTypeConfiguration<Board>, IEntityTypeConfiguration<BoardColumn>
{
    private static void Columns<T>(EntityTypeBuilder<T> b) where T:class
    {
        foreach(var p in b.Metadata.GetProperties()) p.SetColumnName(System.Text.RegularExpressions.Regex.Replace(p.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
    }
    public void Configure(EntityTypeBuilder<ActivityEvent> b) { b.ToTable("issue_activity"); b.HasKey(e=>e.EventId); b.Property(e=>e.EventId).ValueGeneratedNever(); Columns(b); b.HasIndex(e=>new{e.TenantId,e.EntityType,e.EntityId,e.OccurredAt}); }
    public void Configure(EntityTypeBuilder<IssueWatcher> b) { b.ToTable("issue_watchers"); b.HasKey(e=>new{e.TenantId,e.IssueId,e.UserId}); Columns(b); }
    public void Configure(EntityTypeBuilder<WorkflowRules> b) { b.ToTable("project_workflow_rules"); b.HasKey(e=>new{e.TenantId,e.ProjectId}); b.Property(e=>e.Definition).HasColumnType("jsonb"); Columns(b); b.Property(e=>e.Version).HasColumnName("xmin").IsRowVersion(); }
    public void Configure(EntityTypeBuilder<RequestReplay> b) { b.ToTable("request_replays"); b.HasKey(e=>new{e.TenantId,e.UserId,e.Key}); b.Property(e=>e.Key).HasMaxLength(128); Columns(b); b.Property(e=>e.ETag).HasColumnName("etag"); }
    public void Configure(EntityTypeBuilder<Board> b) { b.ToTable("boards"); b.HasKey(e=>e.Id); b.Property(e=>e.FilterDefinition).HasColumnType("jsonb"); b.HasOne<Jira.Domain.Entities.Project>().WithMany().HasForeignKey(e=>e.ProjectId).OnDelete(DeleteBehavior.Cascade); Columns(b); }
    public void Configure(EntityTypeBuilder<BoardColumn> b) { b.ToTable("board_columns"); b.HasKey(e=>e.Id); b.HasOne<Board>().WithMany().HasForeignKey(e=>e.BoardId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(e=>new{e.BoardId,e.Position}).IsUnique(); b.HasIndex(e=>new{e.BoardId,e.StatusCode}).IsUnique(); Columns(b); }
}

using Jira.Infrastructure.Persistence.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Jira.Infrastructure.Persistence.Configurations;
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(n => n.EventId).HasColumnName("event_id");
        b.Property(n => n.TenantId).HasColumnName("tenant_id");
        b.Property(n => n.RecipientUserId).HasColumnName("recipient_user_id");
        b.Property(n => n.ActorUserId).HasColumnName("actor_user_id");
        b.Property(n => n.TimesheetPeriodId).HasColumnName("timesheet_period_id");
        b.Property(n => n.Reason).HasColumnName("reason").HasMaxLength(2000);
        b.Property(n => n.IssueId).HasColumnName("issue_id");
        b.Property(n => n.Kind).HasColumnName("kind").HasMaxLength(40).IsRequired();
        b.Property(n => n.CreatedAt).HasColumnName("created_at");
        b.Property(n => n.ReadAt).HasColumnName("read_at");
        b.HasIndex(n => new { n.TenantId, n.EventId, n.RecipientUserId }).IsUnique().HasDatabaseName("ux_notifications_event_recipient");
        b.HasIndex(n => new { n.TenantId, n.RecipientUserId, n.CreatedAt }).HasDatabaseName("ix_notifications_inbox");
    }
}

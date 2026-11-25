using Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Audit.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration
    : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(
        EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(x => x.ActorUserId)
            .HasColumnName("actor_user_id");

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100);

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id");

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(50);

        builder.Property(x => x.OldValues)
            .HasColumnName("old_values")
            .HasColumnType("jsonb");

        builder.Property(x => x.NewValues)
            .HasColumnName("new_values")
            .HasColumnType("jsonb");

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.HasIndex(x => x.TenantId);

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ActorUserId
        });

        builder.HasIndex(x => new
        {
            x.EntityType,
            x.EntityId
        });

        builder.HasIndex(x => x.OccurredAt);
        
        builder.Property(x => x.EventId)
            .HasColumnName("event_id")
            .HasColumnType("uuid");

        builder.HasIndex(x => x.EventId)
            .HasDatabaseName("ux_audit_logs_event_id")
            .IsUnique()
            .HasFilter("event_id IS NOT NULL");

    }
}
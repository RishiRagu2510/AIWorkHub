using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jira.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(message => message.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(message => message.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(message => message.Topic)
            .HasColumnName("topic")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(message => message.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(message => message.NextAttemptAt)
            .HasColumnName("next_attempt_at");

        builder.Property(message => message.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(message => message.LastError)
            .HasColumnName("last_error");

        builder.Property(message => message.LockedUntil)
            .HasColumnName("locked_until");

        builder.Property(message => message.LockedBy)
            .HasColumnName("locked_by")
            .HasMaxLength(100);

        builder.HasIndex(message => message.EventId)
            .IsUnique()
            .HasDatabaseName("uq_outbox_messages_event_id");

        builder.HasIndex(message => new
            {
                message.ProcessedAt,
                message.NextAttemptAt,
                message.LockedUntil,
                message.Id
            })
            .HasDatabaseName("idx_outbox_messages_pending_claim");
    }
}
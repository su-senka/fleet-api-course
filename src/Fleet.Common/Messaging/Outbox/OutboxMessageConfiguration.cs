using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Common.Messaging.Outbox;

/// <summary>
/// Maps the <c>outbox</c> table. Every publishing module applies this to its own
/// <c>DbContext</c>, which puts one table in each module's schema.
/// </summary>
/// <remarks>
/// There is no shared outbox table, for the same reason there are no cross-schema foreign keys.
/// The outbox row and the state change it describes have to be written in one transaction, and a
/// transaction cannot span two modules' units of work.
/// </remarks>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.Type).HasMaxLength(400).IsRequired();

        // jsonb rather than text: it is queryable when somebody inevitably needs to find the one
        // message about a particular driver, and it costs nothing here.
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();

        builder.Property(message => message.OccurredAt).IsRequired();
        builder.Property(message => message.ProcessedAt);
        builder.Property(message => message.AttemptCount).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(2000);

        // The dispatcher asks one question, every few seconds, forever: "what is still pending,
        // oldest first?". A partial index answers it without scanning the processed rows, which
        // are the ones that pile up.
        builder.HasIndex(message => message.OccurredAt)
            .HasFilter("processed_at IS NULL")
            .HasDatabaseName("ix_outbox_pending");
    }
}

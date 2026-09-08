using Fleet.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Notifications.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();

        builder.Property(notification => notification.Kind).IsRequired();
        builder.Property(notification => notification.SubjectId).IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(Notification.MessageMaxLength)
            .IsRequired();

        builder.Property(notification => notification.CreatedAt).IsRequired();
        builder.Property(notification => notification.ReadAt);

        // The outbox promises at-least-once delivery, so the same event can arrive twice. This
        // index is what makes the handler's "have I already recorded this?" check cheap, and
        // unique so that two concurrent deliveries cannot both get past it.
        builder.HasIndex(notification => new { notification.Kind, notification.SubjectId })
            .IsUnique();

        builder.HasIndex(notification => notification.CreatedAt);
    }
}

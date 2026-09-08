using Fleet.Modules.Notifications.Contracts;
using Fleet.Modules.Notifications.Domain;

namespace Fleet.Modules.Notifications.Tests;

/// <summary>
/// The whole of the Notifications domain, which is deliberately almost nothing.
/// </summary>
/// <remarks>
/// The brief is explicit that a row and a log line are the entire implementation. Resisting the
/// urge to add templates, channels and delivery receipts is the design decision here, and there is
/// correspondingly little to test.
/// </remarks>
public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static Notification New(string message = "Licence expires soon.") =>
        Notification.Create(
            Guid.CreateVersion7(), NotificationKind.CertificateExpiring, Guid.CreateVersion7(), message, Now);

    [Fact]
    public void A_new_notification_is_unread()
    {
        var notification = New();

        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void Marking_read_records_when()
    {
        var notification = New();

        notification.MarkRead(Now.AddHours(2));

        Assert.True(notification.IsRead);
        Assert.Equal(Now.AddHours(2), notification.ReadAt);
    }

    [Fact]
    public void Marking_read_twice_keeps_the_first_time()
    {
        // Idempotent on purpose. "Mark as read" is exactly the request a client fires twice
        // because somebody double-clicked, and there is nothing here worth a 409.
        var notification = New();

        notification.MarkRead(Now);
        notification.MarkRead(Now.AddDays(1));

        Assert.Equal(Now, notification.ReadAt);
    }

    [Fact]
    public void A_long_message_is_truncated_rather_than_rejected()
    {
        // The message is generated from an event, not typed by a person. Failing the handler
        // because a driver has a long name would lose the notification entirely, which is a much
        // worse outcome than a clipped sentence.
        var notification = New(new string('x', Notification.MessageMaxLength + 200));

        Assert.Equal(Notification.MessageMaxLength, notification.Message.Length);
    }

    [Fact]
    public void The_message_is_trimmed()
    {
        Assert.Equal("Licence expires soon.", New("   Licence expires soon.  ").Message);
    }
}

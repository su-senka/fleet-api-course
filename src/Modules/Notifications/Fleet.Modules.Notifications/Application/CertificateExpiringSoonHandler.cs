using Fleet.Common.Messaging;
using Fleet.Common.Time;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Notifications.Contracts;
using Fleet.Modules.Notifications.Domain;
using Fleet.Modules.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Modules.Notifications.Application;

/// <summary>
/// Records a notification when the Drivers module says a certificate is about to expire.
/// </summary>
/// <remarks>
/// <para>
/// The subscriber half of the outbox pattern, and the only way anything ever gets into the
/// notifications table. Note the direction of the dependency: Notifications references
/// <c>Fleet.Modules.Drivers.Contracts</c> to know what the event looks like. Drivers has never
/// heard of Notifications and would carry on working perfectly if this module were deleted.
/// </para>
/// <para>
/// The duplicate check is not optional. The outbox guarantees at-least-once delivery, so this
/// handler <em>will</em> see the same event twice sooner or later - a process that dies between
/// publishing and marking the row processed is all it takes.
/// </para>
/// </remarks>
internal sealed class CertificateExpiringSoonHandler(
    NotificationsDbContext dbContext,
    IClock clock,
    ILogger<CertificateExpiringSoonHandler> logger) : IIntegrationEventHandler<CertificateExpiringSoon>
{
    public async Task HandleAsync(
        CertificateExpiringSoon integrationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var alreadyRecorded = await dbContext.Notifications.AnyAsync(
            notification =>
                notification.Kind == NotificationKind.CertificateExpiring
                && notification.SubjectId == integrationEvent.CertificateId,
            cancellationToken);

        if (alreadyRecorded)
        {
            logger.LogDebug(
                "Certificate {CertificateId} has already been notified about; ignoring the repeat",
                integrationEvent.CertificateId);

            return;
        }

        var message =
            $"{integrationEvent.DriverName} ({integrationEvent.EmployeeNumber}): "
            + $"{integrationEvent.Kind} certificate expires on {integrationEvent.ExpiresOn:yyyy-MM-dd}, "
            + $"in {integrationEvent.DaysRemaining} day(s).";

        dbContext.Notifications.Add(Notification.Create(
            Guid.CreateVersion7(),
            NotificationKind.CertificateExpiring,
            integrationEvent.CertificateId,
            message,
            clock.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);

        // The log line the brief asks for. In a real system this is where an email would go; here
        // the point is that it is Notifications' decision what to do, not the publisher's.
        logger.LogInformation("Notification recorded: {Message}", message);
    }
}

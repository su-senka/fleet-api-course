namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Notifications: the smallest resource, and the last one.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>INotificationService</c> in
/// <c>Fleet.Modules.Notifications.Contracts</c>. Note what is missing from it: there is no
/// <c>CreateAsync</c>. Nothing creates a notification by calling this module. They appear because
/// the Drivers module announced that a certificate was expiring, and Notifications happened to be
/// listening.
/// </para>
/// <para>
/// That makes it a read-and-acknowledge resource: list, fetch, mark as read. Marking something
/// read twice is deliberately not an error, which is worth contrasting with cancelling a booking
/// twice - the domain calls that a conflict, and the endpoint answers 204 anyway.
/// </para>
/// <para>
/// The table starts empty. Run the API, wait for the certificate expiry scan and the first outbox
/// sweep, and rows appear on their own.
/// </para>
/// </remarks>
internal static class NotificationEndpoints
{
    // TODO(week-14): map GET /notifications, GET /notifications/{id}, and marking one as read.
    //
    // See docs/assignments/week-14.md.
    //
    // Which verb marks something as read? POST to a sub-resource, PATCH the notification, PUT to
    // /notifications/{id}/read? It is idempotent whichever you choose, which rules some of them
    // in and none of them out.
}

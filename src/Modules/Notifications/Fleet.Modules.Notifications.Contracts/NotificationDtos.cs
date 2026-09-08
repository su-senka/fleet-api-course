namespace Fleet.Modules.Notifications.Contracts;

/// <summary>
/// One notification.
/// </summary>
/// <param name="SubjectId">
/// The thing the notification is about - a certificate id, here. Kept so an endpoint can link
/// back to it, and so a duplicate event does not produce a duplicate row.
/// </param>
public sealed record NotificationDto(
    Guid Id,
    NotificationKind Kind,
    Guid SubjectId,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

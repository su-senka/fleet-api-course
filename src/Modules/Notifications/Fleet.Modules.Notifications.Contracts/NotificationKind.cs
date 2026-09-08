namespace Fleet.Modules.Notifications.Contracts;

/// <summary>What a notification is about.</summary>
public enum NotificationKind
{
    /// <summary>A driver's certificate is within 30 days of expiring.</summary>
    CertificateExpiring = 1,
}

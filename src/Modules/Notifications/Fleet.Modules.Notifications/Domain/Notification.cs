using Fleet.Modules.Notifications.Contracts;

namespace Fleet.Modules.Notifications.Domain;

/// <summary>
/// A recorded notification.
/// </summary>
/// <remarks>
/// The whole module, near enough. The brief is explicit that a row and a log line are the entire
/// implementation - no email, no push, no template engine. Adding any of those would be a lot of
/// code teaching nothing about REST.
/// </remarks>
internal sealed class Notification
{
    private Notification() => Message = string.Empty;

    private Notification(Guid id, NotificationKind kind, Guid subjectId, string message, DateTimeOffset createdAt)
    {
        Id = id;
        Kind = kind;
        SubjectId = subjectId;
        Message = message;
        CreatedAt = createdAt;
    }

    public const int MessageMaxLength = 500;

    public Guid Id { get; private set; }

    public NotificationKind Kind { get; private set; }

    /// <summary>What this is about. Another module's id, so no foreign key, as everywhere.</summary>
    public Guid SubjectId { get; private set; }

    public string Message { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid id,
        NotificationKind kind,
        Guid subjectId,
        string message,
        DateTimeOffset createdAt)
    {
        var trimmed = message.Trim();

        return new Notification(
            id,
            kind,
            subjectId,
            trimmed.Length > MessageMaxLength ? trimmed[..MessageMaxLength] : trimmed,
            createdAt);
    }

    /// <summary>
    /// Marks it read, keeping the first timestamp.
    /// </summary>
    /// <remarks>
    /// Idempotent on purpose. "Mark as read" is the kind of request a client fires twice because
    /// somebody double-clicked, and there is nothing to conflict about.
    /// </remarks>
    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;
}

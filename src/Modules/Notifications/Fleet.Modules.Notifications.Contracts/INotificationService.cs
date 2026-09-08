using Fleet.Common.Paging;
using Fleet.Common.Results;

namespace Fleet.Modules.Notifications.Contracts;

/// <summary>
/// Reads the notifications this module has recorded.
/// </summary>
/// <remarks>
/// There is no <c>SendAsync</c> and no <c>CreateAsync</c>. Notifications are not created by
/// anybody calling this module; they appear because another module announced something. The only
/// way in is the integration event handler, which is exactly the point of the pattern.
/// </remarks>
public interface INotificationService
{
    /// <summary>
    /// One page of notifications, newest first.
    /// </summary>
    /// <param name="filter">Understands <c>kind</c> and <c>unread</c> (<c>true</c>/<c>false</c>).</param>
    Task<Result<PagedResult<NotificationDto>>> ListAsync(
        PageRequest page,
        FilterRequest filter,
        CancellationToken cancellationToken = default);

    Task<Result<NotificationDto>> GetAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>Marks one as read. Marking it twice is not an error - it is already true.</summary>
    Task<Result<NotificationDto>> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
}

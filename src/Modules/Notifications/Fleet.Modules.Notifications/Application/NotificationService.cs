using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Common.Time;
using Fleet.Modules.Notifications.Contracts;
using Fleet.Modules.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Notifications.Application;

internal sealed class NotificationService(NotificationsDbContext dbContext, IClock clock) : INotificationService
{
    public async Task<Result<PagedResult<NotificationDto>>> ListAsync(
        PageRequest page,
        FilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        var query = dbContext.Notifications.AsNoTracking();

        if (filter.EnumTerm<NotificationKind>("kind") is { } kind)
        {
            query = query.Where(notification => notification.Kind == kind);
        }

        if (bool.TryParse(filter["unread"], out var unread))
        {
            query = unread
                ? query.Where(notification => notification.ReadAt == null)
                : query.Where(notification => notification.ReadAt != null);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenBy(notification => notification.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.Kind,
                notification.SubjectId,
                notification.Message,
                notification.CreatedAt,
                notification.ReadAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<NotificationDto>> GetAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == notificationId, cancellationToken);

        return notification is null
            ? NotFound(notificationId)
            : ToDto(notification);
    }

    public async Task<Result<NotificationDto>> MarkReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(candidate => candidate.Id == notificationId, cancellationToken);

        if (notification is null)
        {
            return NotFound(notificationId);
        }

        // Idempotent: marking an already-read notification read again succeeds and keeps the
        // original timestamp. There is no state machine here worth defending.
        notification.MarkRead(clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(notification);
    }

    private static NotificationDto ToDto(Domain.Notification notification) =>
        new(notification.Id,
            notification.Kind,
            notification.SubjectId,
            notification.Message,
            notification.CreatedAt,
            notification.ReadAt);

    private static Error NotFound(Guid notificationId) =>
        Error.NotFound("notification.not_found", $"There is no notification with id {notificationId}.");
}

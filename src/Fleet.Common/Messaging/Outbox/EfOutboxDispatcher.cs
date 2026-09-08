using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Common.Messaging.Outbox;

/// <summary>
/// The read side of a module's outbox: takes pending messages and publishes them.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is <b>at least once</b>, not exactly once. A message can be published and then the
/// process can die before the row is marked processed, in which case it goes out again. Handlers
/// must therefore tolerate seeing the same event twice - which is a property of every real message
/// system too, and the reason <c>Notifications</c> checks before it inserts.
/// </para>
/// <para>
/// Note the ordering inside the loop: publish first, then mark. The other way round would give
/// at-most-once delivery, quietly losing messages whenever the publish failed.
/// </para>
/// </remarks>
public sealed class EfOutboxDispatcher<TDbContext>(
    TDbContext dbContext,
    IEventBus eventBus,
    ILogger<EfOutboxDispatcher<TDbContext>> logger) : IOutboxDispatcher
    where TDbContext : DbContext
{
    /// <summary>The owning module's schema, which is how the logs tell six dispatchers apart.</summary>
    public string ModuleName { get; } = dbContext.Model.GetDefaultSchema() ?? typeof(TDbContext).Name;

    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.Set<OutboxMessage>()
            .Where(message => message.ProcessedAt == null)
            .OrderBy(message => message.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var published = 0;

        foreach (var message in pending)
        {
            var integrationEvent = OutboxSerializer.Deserialize(message);

            if (integrationEvent is null)
            {
                // The event type has been renamed or removed since this row was written. Retrying
                // will never help, so it is recorded and skipped rather than blocking the queue.
                logger.LogError(
                    "Outbox message {MessageId} in {Module} names the unknown type {Type}",
                    message.Id,
                    ModuleName,
                    message.Type);

                message.MarkFailed($"Unknown event type '{message.Type}'.");
                continue;
            }

            try
            {
                await eventBus.PublishAsync(integrationEvent, cancellationToken);

                message.MarkProcessed(DateTimeOffset.UtcNow);
                published++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Left pending on purpose. The next sweep tries again, and AttemptCount plus
                // LastError make a stuck message visible from the table alone.
                logger.LogWarning(
                    exception,
                    "Outbox message {MessageId} in {Module} could not be published (attempt {Attempt})",
                    message.Id,
                    ModuleName,
                    message.AttemptCount + 1);

                message.MarkFailed(exception.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return published;
    }
}

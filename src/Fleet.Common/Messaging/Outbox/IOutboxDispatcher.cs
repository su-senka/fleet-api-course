namespace Fleet.Common.Messaging.Outbox;

/// <summary>
/// The read side of a module's outbox, driven by a background service.
/// </summary>
/// <remarks>
/// One implementation per module, because each module's messages live in its own schema. The
/// hosted service that drives them does not know how many modules there are - it resolves every
/// registered dispatcher and gives each one a turn.
/// </remarks>
public interface IOutboxDispatcher
{
    /// <summary>The owning module, used for logging and metrics. For example <c>bookings</c>.</summary>
    string ModuleName { get; }

    /// <summary>
    /// Publishes up to <paramref name="batchSize"/> pending messages and returns how many were
    /// successfully published.
    /// </summary>
    Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken = default);
}

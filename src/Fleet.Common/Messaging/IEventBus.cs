namespace Fleet.Common.Messaging;

/// <summary>
/// Delivers integration events to their handlers.
/// </summary>
/// <remarks>
/// The implementation in this repository is an in-process dispatcher. There is no broker, no
/// network hop and no at-least-once delivery guarantee beyond what the outbox provides. Modules
/// are written against this interface so that swapping in a real broker later is a change to one
/// class rather than to six modules - but making that swap is not part of this course.
/// </remarks>
public interface IEventBus
{
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);

    Task PublishAsync(IEnumerable<IntegrationEvent> integrationEvents, CancellationToken cancellationToken = default);
}

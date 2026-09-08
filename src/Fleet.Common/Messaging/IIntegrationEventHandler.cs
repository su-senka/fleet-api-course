namespace Fleet.Common.Messaging;

/// <summary>
/// Handles one integration event type. Register one per module that cares about the event.
/// </summary>
/// <remarks>
/// Handlers must tolerate being called twice with the same event: the outbox guarantees the
/// event is delivered at least once, not exactly once.
/// </remarks>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}

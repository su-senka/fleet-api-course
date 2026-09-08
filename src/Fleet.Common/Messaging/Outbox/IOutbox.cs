namespace Fleet.Common.Messaging.Outbox;

/// <summary>
/// The write side of a module's outbox.
/// </summary>
/// <remarks>
/// <see cref="Enqueue"/> does not touch the database on its own. It stages the row on the
/// module's change tracker so that the caller's <see cref="Persistence.IUnitOfWork.SaveChangesAsync"/>
/// commits the event and the state change together, in one transaction. Calling
/// <c>Enqueue</c> and then forgetting to save is the same as never having raised the event.
/// </remarks>
public interface IOutbox
{
    void Enqueue(IntegrationEvent integrationEvent);
}

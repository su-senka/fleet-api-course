using Microsoft.EntityFrameworkCore;

namespace Fleet.Common.Messaging.Outbox;

/// <summary>
/// The write side of a module's outbox, over its own <c>DbContext</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Enqueue"/> only stages the row. It is the caller's <c>SaveChangesAsync</c> that
/// commits the event alongside the state change that caused it, in one transaction. That is the
/// entire point of the pattern: either the certificate is marked as warned about and the
/// <c>CertificateExpiringSoon</c> row exists, or neither does.
/// </para>
/// <para>
/// Publishing straight to <see cref="IEventBus"/> instead would be simpler and wrong. A crash
/// between the commit and the publish loses the event with no trace; a failure in the handler
/// rolls back work the publisher had already committed.
/// </para>
/// </remarks>
public sealed class EfOutbox<TDbContext>(TDbContext dbContext) : IOutbox
    where TDbContext : DbContext
{
    public void Enqueue(IntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var message = new OutboxMessage(
            integrationEvent.Id,
            OutboxSerializer.TypeNameOf(integrationEvent),
            OutboxSerializer.Serialize(integrationEvent),
            integrationEvent.OccurredAt);

        dbContext.Set<OutboxMessage>().Add(message);
    }
}

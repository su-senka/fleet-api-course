namespace Fleet.Common.Messaging.Outbox;

/// <summary>
/// One integration event, serialised and stored in the publishing module's own schema.
/// </summary>
/// <remarks>
/// <para>
/// The point of the outbox: the event row is written in the <em>same transaction</em> as the
/// state change that caused it. Either the booking is cancelled and the
/// <c>BookingCancelled</c> row exists, or neither does. A background dispatcher then publishes
/// the stored rows. Without this, a crash between <c>SaveChanges</c> and <c>Publish</c> loses
/// the event silently.
/// </para>
/// <para>
/// Each module owns its own <c>outbox</c> table inside its own schema. There is no shared outbox,
/// for the same reason there are no cross-schema foreign keys.
/// </para>
/// </remarks>
public sealed class OutboxMessage
{
    // EF Core materialises entities through this constructor.
    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    /// <summary>The event's concrete type name, as written by <see cref="IntegrationEvent.EventType"/>.</summary>
    public string Type { get; private set; }

    /// <summary>The serialised event body, stored as JSON.</summary>
    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When the dispatcher successfully published it. <c>null</c> means "still pending".</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    public int AttemptCount { get; private set; }

    /// <summary>The last failure message, kept so a stuck message can be diagnosed from the table.</summary>
    public string? LastError { get; private set; }

    public bool IsPending => ProcessedAt is null;

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        AttemptCount++;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        AttemptCount++;

        // Postgres text columns have no practical length limit, but an unbounded stack trace in
        // a hot-polled table is not worth the storage.
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}

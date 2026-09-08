namespace Fleet.Common.Messaging;

/// <summary>
/// A fact that one module announces and others may react to.
/// </summary>
/// <remarks>
/// <para>
/// Integration events are past tense and immutable: <c>CertificateExpiringSoon</c>, not
/// <c>SendExpiryWarning</c>. The publisher states what happened; it does not instruct anyone.
/// </para>
/// <para>
/// They are also part of a module's public contract, so they live in <c>*.Contracts</c>
/// alongside the interfaces. Changing a property is a breaking change for every subscriber.
/// </para>
/// </remarks>
public abstract record IntegrationEvent
{
    /// <summary>
    /// Unique id for this occurrence. Version 7 GUIDs are time-ordered, which keeps the outbox
    /// table's primary-key index from fragmenting the way random GUIDs do.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>The concrete type name, used to route the event back from its stored JSON.</summary>
    public string EventType => GetType().Name;
}

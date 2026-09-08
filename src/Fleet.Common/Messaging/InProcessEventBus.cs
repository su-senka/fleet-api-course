using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fleet.Common.Messaging;

/// <summary>
/// Delivers integration events to handlers in this process, by resolving them from the container.
/// </summary>
/// <remarks>
/// <para>
/// There is no broker here and no network hop. An event published by Drivers is handled by
/// Notifications a microsecond later, in the same process, and if Notifications throws then the
/// publisher finds out. That is a long way from how a message bus behaves, and pretending
/// otherwise would teach the wrong lesson.
/// </para>
/// <para>
/// What it does buy is the shape of the thing: publishers name a fact, subscribers react, and
/// neither knows the other exists. Swapping this for a real broker later is a change to this one
/// class plus a lot of thinking about delivery guarantees - not a change to six modules.
/// </para>
/// <para>
/// Handlers are resolved in their own scope, so one module's failure cannot poison another
/// module's <c>DbContext</c>, and so a handler gets a fresh unit of work rather than sharing the
/// publisher's.
/// </para>
/// </remarks>
internal sealed class InProcessEventBus(
    IServiceScopeFactory scopeFactory,
    ILogger<InProcessEventBus> logger) : IEventBus
{
    /// <summary>
    /// One per event type, built once and cached.
    /// </summary>
    /// <remarks>
    /// The bus holds an <see cref="IntegrationEvent"/> and needs to call
    /// <c>IIntegrationEventHandler&lt;TConcrete&gt;.HandleAsync</c>, which the compiler cannot
    /// resolve without knowing <c>TConcrete</c>. Closing a generic dispatcher over the runtime type
    /// bridges the gap and calls the handler normally - so an exception it throws arrives as
    /// itself, rather than wrapped in a <see cref="System.Reflection.TargetInvocationException"/>
    /// that says nothing useful in a log or in the outbox's LastError column.
    /// </remarks>
    private abstract class HandlerDispatcher
    {
        public abstract Type HandlerType { get; }

        public abstract Task InvokeAsync(
            object handler,
            IntegrationEvent integrationEvent,
            CancellationToken cancellationToken);
    }

    private sealed class HandlerDispatcher<TEvent> : HandlerDispatcher
        where TEvent : IntegrationEvent
    {
        public override Type HandlerType => typeof(IIntegrationEventHandler<TEvent>);

        public override Task InvokeAsync(
            object handler,
            IntegrationEvent integrationEvent,
            CancellationToken cancellationToken) =>
            ((IIntegrationEventHandler<TEvent>)handler).HandleAsync((TEvent)integrationEvent, cancellationToken);
    }

    private static readonly ConcurrentDictionary<Type, HandlerDispatcher> Dispatchers = new();

    private static HandlerDispatcher DispatcherFor(Type eventType) =>
        Dispatchers.GetOrAdd(eventType, static type =>
            (HandlerDispatcher)Activator.CreateInstance(typeof(HandlerDispatcher<>).MakeGenericType(type))!);

    public async Task PublishAsync(
        IntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var dispatcher = DispatcherFor(integrationEvent.GetType());

        await using var scope = scopeFactory.CreateAsyncScope();

        var handlers = scope.ServiceProvider.GetServices(dispatcher.HandlerType).ToList();

        if (handlers.Count == 0)
        {
            // Not a problem. An event nobody listens to is a fact nobody needed, and the publisher
            // is not supposed to know or care either way.
            logger.LogDebug(
                "No handler is registered for {EventType} {EventId}",
                integrationEvent.EventType,
                integrationEvent.Id);

            return;
        }

        foreach (var handler in handlers)
        {
            logger.LogDebug(
                "Dispatching {EventType} {EventId} to {Handler}",
                integrationEvent.EventType,
                integrationEvent.Id,
                handler!.GetType().Name);

            // Deliberately not caught. A handler that fails must fail the dispatch, so the outbox
            // leaves the message pending and tries again rather than losing it.
            await dispatcher.InvokeAsync(handler, integrationEvent, cancellationToken);
        }
    }

    public async Task PublishAsync(
        IEnumerable<IntegrationEvent> integrationEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvents);

        foreach (var integrationEvent in integrationEvents)
        {
            await PublishAsync(integrationEvent, cancellationToken);
        }
    }
}

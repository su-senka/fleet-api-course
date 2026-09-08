using Fleet.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fleet.Common.Tests;

/// <summary>
/// The in-process dispatcher: who gets called, and what happens when they throw.
/// </summary>
/// <remarks>
/// No database anywhere. The bus is pure routing, and testing it against a container would prove
/// nothing extra while taking a thousand times as long.
/// </remarks>
public sealed class InProcessEventBusTests
{
    private sealed class RecordingHandler : IIntegrationEventHandler<SomethingHappened>
    {
        public List<SomethingHappened> Handled { get; } = [];

        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken cancellationToken = default)
        {
            Handled.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class OtherHandler : IIntegrationEventHandler<SomethingElseHappened>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(SomethingElseHappened integrationEvent, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IIntegrationEventHandler<SomethingHappened>
    {
        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the handler fell over");
    }

    private static (IEventBus Bus, ServiceProvider Provider) BuildBus(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddFleetCommon();
        register(services);

        var provider = services.BuildServiceProvider();

        return (provider.GetRequiredService<IEventBus>(), provider);
    }

    [Fact]
    public async Task An_event_reaches_its_handler()
    {
        var handler = new RecordingHandler();
        var (bus, provider) = BuildBus(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(handler));

        await using (provider)
        {
            await bus.PublishAsync(new SomethingHappened("booked", 1), TestContext.Current.CancellationToken);
        }

        var received = Assert.Single(handler.Handled);
        Assert.Equal("booked", received.What);
    }

    [Fact]
    public async Task An_event_with_no_handler_is_not_an_error()
    {
        // A publisher does not know or care whether anybody is listening. If nobody subscribes to
        // CertificateExpiringSoon, Drivers must carry on exactly as before.
        var (bus, provider) = BuildBus(_ => { });

        await using (provider)
        {
            await bus.PublishAsync(new SomethingHappened("ignored", 1), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Handlers_are_matched_by_event_type()
    {
        var wanted = new RecordingHandler();
        var unwanted = new OtherHandler();

        var (bus, provider) = BuildBus(services =>
        {
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(wanted);
            services.AddSingleton<IIntegrationEventHandler<SomethingElseHappened>>(unwanted);
        });

        await using (provider)
        {
            await bus.PublishAsync(new SomethingHappened("one", 1), TestContext.Current.CancellationToken);
        }

        Assert.Single(wanted.Handled);
        Assert.Equal(0, unwanted.Calls);
    }

    [Fact]
    public async Task Every_handler_for_an_event_is_called()
    {
        var first = new RecordingHandler();
        var second = new RecordingHandler();

        var (bus, provider) = BuildBus(services =>
        {
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(first);
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(second);
        });

        await using (provider)
        {
            await bus.PublishAsync(new SomethingHappened("both", 1), TestContext.Current.CancellationToken);
        }

        Assert.Single(first.Handled);
        Assert.Single(second.Handled);
    }

    [Fact]
    public async Task A_failing_handler_fails_the_publish()
    {
        // This is what leaves the outbox row pending so the dispatcher retries it. Swallowing the
        // exception here would mark the message processed and lose the event silently.
        var (bus, provider) = BuildBus(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>, ThrowingHandler>());

        await using (provider)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => bus.PublishAsync(new SomethingHappened("boom", 1), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Publishing_a_batch_delivers_all_of_them()
    {
        var handler = new RecordingHandler();
        var (bus, provider) = BuildBus(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(handler));

        await using (provider)
        {
            await bus.PublishAsync(
                [new SomethingHappened("a", 1), new SomethingHappened("b", 2)],
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, handler.Handled.Count);
    }
}

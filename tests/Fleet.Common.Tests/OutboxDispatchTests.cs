using Fleet.Common.Messaging;
using Fleet.Common.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fleet.Common.Tests;

/// <summary>
/// The outbox: writing a message with the change that caused it, and publishing it afterwards.
/// </summary>
/// <remarks>
/// Against a real Postgres, because the property being demonstrated is transactional - that the
/// event and the state change commit together or not at all - and a fake would make that
/// vacuously true.
/// </remarks>
public sealed class OutboxDispatchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
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

    private sealed class ThrowingHandler : IIntegrationEventHandler<SomethingHappened>
    {
        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the subscriber is having a bad day");
    }

    [Fact]
    public async Task Enqueue_does_not_write_until_the_caller_saves()
    {
        // The heart of the pattern. Enqueue only stages the row; it is the module's own
        // SaveChanges that commits the event alongside whatever else changed.
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await using (var scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IOutbox>()
                .Enqueue(new SomethingHappened("staged", 1));

            // Scope disposed without SaveChanges: nothing should have reached the database.
        }

        Assert.Equal(0, await CountAsync(provider));
    }

    [Fact]
    public async Task A_saved_message_is_pending_until_it_is_dispatched()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await EnqueueAndSaveAsync(provider, new SomethingHappened("booked", 1));

        var stored = await SingleMessageAsync(provider);

        Assert.True(stored.IsPending);
        Assert.Null(stored.ProcessedAt);
        Assert.Equal(0, stored.AttemptCount);
    }

    [Fact]
    public async Task Dispatching_publishes_the_message_and_marks_it_processed()
    {
        postgres.SkipIfUnavailable();

        var handler = new RecordingHandler();
        await using var provider = await BuildProviderAsync(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(handler));

        await EnqueueAndSaveAsync(provider, new SomethingHappened("booked", 7));

        var published = await DispatchAsync(provider);

        Assert.Equal(1, published);

        var received = Assert.Single(handler.Handled);
        Assert.Equal("booked", received.What);
        Assert.Equal(7, received.Count);

        var stored = await SingleMessageAsync(provider);
        Assert.False(stored.IsPending);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Null(stored.LastError);
    }

    [Fact]
    public async Task A_processed_message_is_not_published_twice()
    {
        postgres.SkipIfUnavailable();

        var handler = new RecordingHandler();
        await using var provider = await BuildProviderAsync(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(handler));

        await EnqueueAndSaveAsync(provider, new SomethingHappened("once", 1));

        await DispatchAsync(provider);
        var second = await DispatchAsync(provider);

        Assert.Equal(0, second);
        Assert.Single(handler.Handled);
    }

    [Fact]
    public async Task A_failing_handler_leaves_the_message_pending_for_another_go()
    {
        // At-least-once delivery. Marking it processed anyway would be at-most-once, which loses
        // the event the moment a subscriber has a bad minute.
        postgres.SkipIfUnavailable();

        await using var provider = await BuildProviderAsync(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>, ThrowingHandler>());

        await EnqueueAndSaveAsync(provider, new SomethingHappened("doomed", 1));

        var published = await DispatchAsync(provider);

        Assert.Equal(0, published);

        var stored = await SingleMessageAsync(provider);
        Assert.True(stored.IsPending);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Contains("bad day", stored.LastError!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_message_is_retried_and_counts_its_attempts()
    {
        postgres.SkipIfUnavailable();

        await using var provider = await BuildProviderAsync(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>, ThrowingHandler>());

        await EnqueueAndSaveAsync(provider, new SomethingHappened("doomed", 1));

        await DispatchAsync(provider);
        await DispatchAsync(provider);
        await DispatchAsync(provider);

        var stored = await SingleMessageAsync(provider);

        // AttemptCount and LastError are what make a stuck message visible from the table alone,
        // without anybody having to correlate log lines.
        Assert.Equal(3, stored.AttemptCount);
        Assert.True(stored.IsPending);
    }

    [Fact]
    public async Task An_event_with_no_handler_is_still_marked_processed()
    {
        // Nobody subscribed, so there is nothing to retry. Leaving it pending forever would make
        // the table grow without bound and hide the messages that genuinely are stuck.
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await EnqueueAndSaveAsync(provider, new SomethingHappened("unheard", 1));

        Assert.Equal(1, await DispatchAsync(provider));
        Assert.False((await SingleMessageAsync(provider)).IsPending);
    }

    [Fact]
    public async Task Messages_are_dispatched_oldest_first_and_in_batches()
    {
        postgres.SkipIfUnavailable();

        var handler = new RecordingHandler();
        await using var provider = await BuildProviderAsync(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(handler));

        for (var i = 0; i < 5; i++)
        {
            await EnqueueAndSaveAsync(
                provider,
                new SomethingHappened($"event-{i}", i)
                {
                    OccurredAt = DateTimeOffset.UtcNow.AddMinutes(i - 10),
                });
        }

        var first = await DispatchAsync(provider, batchSize: 2);

        Assert.Equal(2, first);
        Assert.Equal(["event-0", "event-1"], handler.Handled.Select(e => e.What));

        await DispatchAsync(provider, batchSize: 50);

        Assert.Equal(5, handler.Handled.Count);
        Assert.Equal(0, await PendingCountAsync(provider));
    }

    [Fact]
    public async Task A_message_naming_an_unknown_type_is_recorded_and_skipped()
    {
        // Somebody renamed an event class while messages were pending. Retrying will never help,
        // so it must not sit at the head of the queue blocking everything behind it.
        postgres.SkipIfUnavailable();

        var handler = new RecordingHandler();
        await using var provider = await BuildProviderAsync(services =>
            services.AddSingleton<IIntegrationEventHandler<SomethingHappened>>(handler));

        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>();

            dbContext.Outbox.Add(new OutboxMessage(
                Guid.CreateVersion7(),
                "Fleet.Modules.Ghost.Contracts.VanishedEvent, Fleet.Modules.Ghost.Contracts",
                "{}",
                DateTimeOffset.UtcNow.AddMinutes(-10)));

            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EnqueueAndSaveAsync(provider, new SomethingHappened("still fine", 1));

        var published = await DispatchAsync(provider);

        // The good message went out even though the bad one was ahead of it in the queue.
        Assert.Equal(1, published);
        Assert.Single(handler.Handled);
    }

    // -----------------------------------------------------------------------------------------

    private static async Task EnqueueAndSaveAsync(ServiceProvider provider, IntegrationEvent integrationEvent)
    {
        await using var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<IOutbox>().Enqueue(integrationEvent);

        await scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> DispatchAsync(ServiceProvider provider, int batchSize = 50)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
            .DispatchPendingAsync(batchSize, TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>()
            .Outbox.CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> PendingCountAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>()
            .Outbox.CountAsync(message => message.ProcessedAt == null, TestContext.Current.CancellationToken);
    }

    private static async Task<OutboxMessage> SingleMessageAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>()
            .Outbox.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ServiceProvider> BuildProviderAsync(Action<IServiceCollection>? register = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Critical));
        services.AddFleetCommon();

        services.AddDbContext<TestOutboxDbContext>(options => options
            .UseNpgsql(postgres.ConnectionString)
            .UseSnakeCaseNamingConvention());

        services.AddModuleOutbox<TestOutboxDbContext>();
        register?.Invoke(services);

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>();

            // Each test gets a clean table. EnsureDeleted would drop the whole database, which the
            // idempotency tests in this class's sibling are also using.
            await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            await dbContext.Outbox.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        return provider;
    }
}

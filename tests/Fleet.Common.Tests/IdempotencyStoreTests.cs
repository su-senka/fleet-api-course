using Fleet.Common.Idempotency;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fleet.Common.Tests;

/// <summary>
/// The idempotency store, against a real Postgres.
/// </summary>
/// <remarks>
/// The property under test is atomicity, which no in-memory fake can demonstrate. A store that
/// looked correct in a unit test and lost the race in production would be worse than none at all,
/// because the whole point of the thing is to stop a retried payment being taken twice.
/// </remarks>
public sealed class IdempotencyStoreTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static string NewKey() => $"key-{Guid.CreateVersion7():N}";

    [Fact]
    public async Task A_key_can_be_claimed_once()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var key = NewKey();

        Assert.True(await ClaimAsync(provider, key));
        Assert.False(await ClaimAsync(provider, key));
    }

    [Fact]
    public async Task Exactly_one_of_many_parallel_claims_wins()
    {
        // The test that matters. Twenty requests arrive at once carrying the same
        // Idempotency-Key; exactly one may do the work.
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var key = NewKey();
        using var gate = new Barrier(20);

        var attempts = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

            gate.SignalAndWait();
            return await store.TryBeginAsync(key, "fingerprint", TestContext.Current.CancellationToken);
        }));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(claimed => claimed));
        Assert.Equal(19, results.Count(claimed => !claimed));
    }

    [Fact]
    public async Task A_claimed_key_starts_in_progress()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var key = NewKey();
        await ClaimAsync(provider, key, "fingerprint-a");

        var record = await GetAsync(provider, key);

        Assert.NotNull(record);
        Assert.Equal(IdempotencyState.InProgress, record.State);
        Assert.Equal("fingerprint-a", record.RequestFingerprint);
        Assert.Null(record.StatusCode);
    }

    [Fact]
    public async Task A_completed_key_replays_its_response()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var key = NewKey();
        await ClaimAsync(provider, key);

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
                .CompleteAsync(key, 201, "{\"id\":\"abc\"}", TestContext.Current.CancellationToken);
        }

        var record = await GetAsync(provider, key);

        Assert.Equal(IdempotencyState.Completed, record!.State);
        Assert.Equal(201, record.StatusCode);
        Assert.Equal("{\"id\":\"abc\"}", record.ResponseBody);
    }

    [Fact]
    public async Task The_fingerprint_of_the_original_request_is_kept()
    {
        // So the middleware can tell "the client retried" from "the client reused a key with a
        // different body", which is a bug on their side and deserves saying so rather than
        // replaying an unrelated response.
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var key = NewKey();
        await ClaimAsync(provider, key, "fingerprint-of-the-first-body");
        await ClaimAsync(provider, key, "fingerprint-of-a-different-body");

        var record = await GetAsync(provider, key);

        Assert.Equal("fingerprint-of-the-first-body", record!.RequestFingerprint);
    }

    [Fact]
    public async Task Abandoning_frees_the_key_for_a_retry()
    {
        // Without this, one 500 would poison the key until the purge job came round, and the
        // client's perfectly reasonable retry would be refused forever.
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var key = NewKey();
        await ClaimAsync(provider, key);

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
                .AbandonAsync(key, TestContext.Current.CancellationToken);
        }

        Assert.True(await ClaimAsync(provider, key));
    }

    [Fact]
    public async Task Abandoning_does_not_discard_a_completed_response()
    {
        // Abandon is for requests that failed. A completed key holds the response a retry is
        // supposed to replay, and deleting it would let the work happen twice.
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var key = NewKey();
        await ClaimAsync(provider, key);

        await using (var scope = provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            await store.CompleteAsync(key, 201, "{}", TestContext.Current.CancellationToken);
            await store.AbandonAsync(key, TestContext.Current.CancellationToken);
        }

        Assert.NotNull(await GetAsync(provider, key));
        Assert.False(await ClaimAsync(provider, key));
    }

    [Fact]
    public async Task An_unknown_key_is_null_rather_than_an_error()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        Assert.Null(await GetAsync(provider, NewKey()));
    }

    [Fact]
    public async Task Completing_a_key_nobody_claimed_is_harmless()
    {
        // It can happen if the purge job ran mid-request. Throwing here would turn a request that
        // succeeded into a 500, which is a strictly worse outcome than doing nothing.
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
            .CompleteAsync(NewKey(), 200, "{}", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Purging_removes_only_the_old_records()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        var recent = NewKey();
        await ClaimAsync(provider, recent);

        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        var removed = await store.PurgeAsync(
            DateTimeOffset.UtcNow.AddDays(-1), TestContext.Current.CancellationToken);

        Assert.Equal(0, removed);
        Assert.NotNull(await store.GetAsync(recent, TestContext.Current.CancellationToken));

        await store.PurgeAsync(DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);

        Assert.Null(await store.GetAsync(recent, TestContext.Current.CancellationToken));
    }

    private static async Task<bool> ClaimAsync(
        ServiceProvider provider,
        string key,
        string fingerprint = "fingerprint")
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
            .TryBeginAsync(key, fingerprint, TestContext.Current.CancellationToken);
    }

    private static async Task<IdempotencyRecord?> GetAsync(ServiceProvider provider, string key)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
            .GetAsync(key, TestContext.Current.CancellationToken);
    }

    private async Task<ServiceProvider> BuildProviderAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Fleet"] = postgres.ConnectionString,
                // Nothing here needs the sweeper, and a background service polling a container
                // that a test is about to dispose is a good way to produce confusing noise.
                ["Outbox:Enabled"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddFleetCommon();
        services.AddFleetInfrastructure(configuration);

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            foreach (var initializer in scope.ServiceProvider.GetServices<Persistence.IModuleDatabaseInitializer>())
            {
                await initializer.MigrateAsync(TestContext.Current.CancellationToken);
            }
        }

        return provider;
    }
}

using Fleet.Common.Persistence;
using Fleet.Common.Time;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Common.Idempotency;

/// <summary>
/// The idempotency store, over Postgres.
/// </summary>
/// <remarks>
/// <para>
/// This class is finished. The interesting part - the middleware that reads the
/// <c>Idempotency-Key</c> header, claims it, replays a stored response and lets a fresh request
/// through - is written once in <c>Fleet.Api</c> as the worked example.
/// </para>
/// <para>
/// The whole design rests on <see cref="TryBeginAsync"/> being atomic, and it is atomic because
/// the key is the primary key: one <c>INSERT ... ON CONFLICT DO NOTHING</c> either claims it or
/// does not. Reading first and then inserting would look equivalent and would lose the race -
/// which, on a payment endpoint, means taking the money twice.
/// </para>
/// </remarks>
internal sealed class PostgresIdempotencyStore(
    FleetInfrastructureDbContext dbContext,
    IClock clock) : IIdempotencyStore
{
    public async Task<bool> TryBeginAsync(
        string key,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Raw SQL, deliberately, and one of the few places in this repository where it beats the
        // ORM outright. INSERT ... ON CONFLICT DO NOTHING is a single atomic statement: it either
        // inserts the row or it does not, and it tells you which by the row count.
        //
        // Going through the change tracker instead would mean Add-then-SaveChanges, catching the
        // unique violation, and then detaching the entity that failed to insert - and it would
        // still throw before reaching the database if this scope had already seen the same key.
        // For a primitive whose entire job is to be atomic, that is a lot of moving parts.
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO shared.idempotency_keys (key, request_fingerprint, state, created_at)
            VALUES ({key}, {requestFingerprint}, {(int)IdempotencyState.InProgress}, {clock.UtcNow})
            ON CONFLICT (key) DO NOTHING
            """,
            cancellationToken);

        return inserted == 1;
    }

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        return record?.ToRecord();
    }

    public async Task CompleteAsync(
        string key,
        int statusCode,
        string? responseBody,
        CancellationToken cancellationToken = default)
    {
        var record = await dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        if (record is null)
        {
            // The key was purged, or never claimed. Nothing to record and nothing to fix: the work
            // is done either way, and failing here would turn a successful request into a 500.
            return;
        }

        record.Complete(statusCode, responseBody, clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AbandonAsync(string key, CancellationToken cancellationToken = default)
    {
        // Without this, one 500 would poison the key forever and the client's perfectly reasonable
        // retry would be told "a request with this key is already in progress" until the purge job
        // came round.
        await dbContext.IdempotencyKeys
            .Where(record => record.Key == key && record.State == IdempotencyState.InProgress)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> PurgeAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyKeys
            .Where(record => record.CreatedAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
}

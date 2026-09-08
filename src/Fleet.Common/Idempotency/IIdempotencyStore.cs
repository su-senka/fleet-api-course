namespace Fleet.Common.Idempotency;

public enum IdempotencyState
{
    /// <summary>A request with this key is being handled right now.</summary>
    InProgress = 0,

    /// <summary>A request with this key finished; the stored response can be replayed.</summary>
    Completed = 1,
}

/// <summary>What happened the last time this <c>Idempotency-Key</c> was seen.</summary>
/// <param name="RequestFingerprint">
/// A hash of the original request body. If a second request reuses the key with a different body,
/// that is a client bug and deserves a 422 - not a replay of an unrelated response.
/// </param>
public sealed record IdempotencyRecord(
    string Key,
    string RequestFingerprint,
    IdempotencyState State,
    int? StatusCode,
    string? ResponseBody,
    DateTimeOffset CreatedAt);

/// <summary>
/// Remembers the outcome of a keyed request so that retrying it does not repeat its side effects.
/// </summary>
/// <remarks>
/// <para>
/// This is the <em>store</em>, and it is finished. The middleware that reads the
/// <c>Idempotency-Key</c> header, calls <see cref="TryBeginAsync"/>, replays a completed response
/// and lets a fresh request through is the interesting part - and it is written for you once in
/// <c>Fleet.Api</c> as the worked example.
/// </para>
/// <para>
/// <see cref="TryBeginAsync"/> must be atomic: two concurrent requests carrying the same key must
/// not both receive <c>true</c>. The Postgres implementation relies on the primary key of the
/// <c>idempotency_keys</c> table to make that guarantee, rather than a check-then-insert that
/// would lose the race.
/// </para>
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Claims the key for a new request. Returns <c>true</c> when the caller now owns it and
    /// should do the work, or <c>false</c> when the key was already taken - in which case
    /// <see cref="GetAsync"/> says whether to replay a response or to answer "still in progress".
    /// </summary>
    Task<bool> TryBeginAsync(
        string key,
        string requestFingerprint,
        CancellationToken cancellationToken = default);

    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores the response so that later requests with the same key can replay it.</summary>
    Task CompleteAsync(
        string key,
        int statusCode,
        string? responseBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a key whose request failed, so the client's retry is allowed to do the work.
    /// Without this, one 500 would poison the key forever.
    /// </summary>
    Task AbandonAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes records older than <paramref name="olderThan"/>. Called by a background job.</summary>
    Task<int> PurgeAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}

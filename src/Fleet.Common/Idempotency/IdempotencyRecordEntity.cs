namespace Fleet.Common.Idempotency;

/// <summary>
/// The stored form of an <c>Idempotency-Key</c> and whatever the request that claimed it produced.
/// </summary>
/// <remarks>
/// Separate from the <see cref="IdempotencyRecord"/> that callers see, because this one is
/// mutable, has a private constructor for EF, and is nobody else's business.
/// </remarks>
internal sealed class IdempotencyRecordEntity
{
    private IdempotencyRecordEntity()
    {
        Key = string.Empty;
        RequestFingerprint = string.Empty;
    }

    public IdempotencyRecordEntity(string key, string requestFingerprint, DateTimeOffset createdAt)
    {
        Key = key;
        RequestFingerprint = requestFingerprint;
        State = IdempotencyState.InProgress;
        CreatedAt = createdAt;
    }

    public const int KeyMaxLength = 200;
    public const int FingerprintMaxLength = 128;

    public string Key { get; private set; }

    public string RequestFingerprint { get; private set; }

    public IdempotencyState State { get; private set; }

    public int? StatusCode { get; private set; }

    public string? ResponseBody { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(int statusCode, string? responseBody, DateTimeOffset completedAt)
    {
        State = IdempotencyState.Completed;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CompletedAt = completedAt;
    }

    public IdempotencyRecord ToRecord() =>
        new(Key, RequestFingerprint, State, StatusCode, ResponseBody, CreatedAt);
}

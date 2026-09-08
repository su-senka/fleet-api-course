using System.Collections.Concurrent;

namespace Supplier.Api.Fake;

/// <summary>How this request should be treated.</summary>
internal enum FakeVerdict
{
    /// <summary>Answer normally.</summary>
    Serve,

    /// <summary>Answer normally, but only after a long pause.</summary>
    ServeSlowly,

    /// <summary>500. The supplier's own fault.</summary>
    ServerError,

    /// <summary>429, with a Retry-After telling the caller how long to wait.</summary>
    RateLimited,

    /// <summary>503. The supplier is down for a while.</summary>
    Outage,
}

/// <param name="Verdict">What to do.</param>
/// <param name="Sequence">Which request this was. The whole decision is a function of it.</param>
/// <param name="RetryAfter">How long to tell the caller to wait, for 429 and 503.</param>
/// <param name="Delay">How long to sleep before answering.</param>
internal sealed record FakeDecision(
    FakeVerdict Verdict,
    long Sequence,
    TimeSpan? RetryAfter = null,
    TimeSpan? Delay = null);

/// <summary>
/// Settings read once from the environment. Defaults are the ones in the README.
/// </summary>
internal sealed record FakeSettings(
    int Seed,
    double ErrorRate,
    double SlowRate,
    int SlowMilliseconds,
    int RateLimitPerMinute,
    int? OutageAfter)
{
    public static FakeSettings FromEnvironment(IConfiguration configuration) => new(
        Seed: configuration.GetValue("FAKE_SEED", 20260101),
        ErrorRate: configuration.GetValue("FAKE_ERROR_RATE", 0.25),
        SlowRate: configuration.GetValue("FAKE_SLOW_RATE", 0.15),
        SlowMilliseconds: configuration.GetValue("FAKE_SLOW_MS", 8000),
        RateLimitPerMinute: configuration.GetValue("FAKE_RATE_LIMIT", 20),
        OutageAfter: configuration.GetValue<int?>("FAKE_OUTAGE_AFTER", null));
}

/// <summary>
/// Decides how badly to behave, reproducibly.
/// </summary>
/// <remarks>
/// <para>
/// The point is that the same seed produces the same sequence of failures on every run, so an
/// experiment can be repeated. That rules out a shared <see cref="Random"/>: two concurrent
/// requests would race for it and the order of the results would differ run to run.
/// </para>
/// <para>
/// Instead every request takes a sequence number, and its treatment is derived by hashing
/// <c>(seed, sequence)</c>. Request 37 gets the same verdict whether it arrives alone or alongside
/// twenty others. The rate limit and the outage window are the exceptions - both are genuinely
/// about wall-clock time, and neither can be made reproducible without lying about what it models.
/// </para>
/// </remarks>
internal sealed class FailureInjector(FakeSettings settings, TimeProvider timeProvider)
{
    private const int ErrorStream = 1;
    private const int SlowStream = 2;

    private static readonly TimeSpan OutageDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();

    private long _sequence;
    private long _windowStartTicks;
    private int _requestsInWindow;
    private long _outageUntilTicks;

    public FakeSettings Settings => settings;

    public FakeDecision Decide()
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var now = timeProvider.GetUtcNow();

        if (CheckOutage(sequence, now) is { } outage)
        {
            return new FakeDecision(FakeVerdict.Outage, sequence, RetryAfter: outage);
        }

        if (CheckRateLimit(now) is { } retryAfter)
        {
            return new FakeDecision(FakeVerdict.RateLimited, sequence, RetryAfter: retryAfter);
        }

        if (Roll(sequence, ErrorStream) < settings.ErrorRate)
        {
            return new FakeDecision(FakeVerdict.ServerError, sequence);
        }

        if (Roll(sequence, SlowStream) < settings.SlowRate)
        {
            return new FakeDecision(
                FakeVerdict.ServeSlowly,
                sequence,
                Delay: TimeSpan.FromMilliseconds(settings.SlowMilliseconds));
        }

        return new FakeDecision(FakeVerdict.Serve, sequence);
    }

    /// <summary>
    /// Starts a one-minute outage every <c>FAKE_OUTAGE_AFTER</c> requests, and reports how much of
    /// the current one is left.
    /// </summary>
    /// <remarks>
    /// Outages recur rather than happening once. A circuit breaker that only ever sees a single
    /// outage never gets to demonstrate the half-open state it exists for.
    /// </remarks>
    private TimeSpan? CheckOutage(long sequence, DateTimeOffset now)
    {
        if (settings.OutageAfter is not { } every || every <= 0)
        {
            return null;
        }

        lock (_gate)
        {
            if (sequence % every == 0)
            {
                _outageUntilTicks = (now + OutageDuration).UtcTicks;
            }

            if (_outageUntilTicks == 0)
            {
                return null;
            }

            var until = new DateTimeOffset(_outageUntilTicks, TimeSpan.Zero);
            return until > now ? until - now : null;
        }
    }

    /// <summary>A fixed-window counter: simple, slightly bursty at the boundary, and honest about it.</summary>
    private TimeSpan? CheckRateLimit(DateTimeOffset now)
    {
        if (settings.RateLimitPerMinute <= 0)
        {
            return null;
        }

        lock (_gate)
        {
            var windowStart = new DateTimeOffset(_windowStartTicks, TimeSpan.Zero);

            if (_windowStartTicks == 0 || now - windowStart >= RateLimitWindow)
            {
                _windowStartTicks = now.UtcTicks;
                _requestsInWindow = 0;
                windowStart = now;
            }

            _requestsInWindow++;

            if (_requestsInWindow <= settings.RateLimitPerMinute)
            {
                return null;
            }

            var resetsAt = windowStart + RateLimitWindow;
            var remaining = resetsAt - now;

            // Never advertise zero: a client that retries immediately just gets another 429.
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1);
        }
    }

    /// <summary>
    /// A value in <c>[0, 1)</c> derived from the seed, the request number and a stream id.
    /// </summary>
    /// <remarks>
    /// SplitMix64. The stream id keeps the "is it an error?" roll independent of the "is it slow?"
    /// roll, so changing one rate does not reshuffle the other.
    /// </remarks>
    private double Roll(long sequence, int stream)
    {
        unchecked
        {
            var x = (ulong)settings.Seed * 0x9E3779B97F4A7C15UL
                    + (ulong)sequence * 0xBF58476D1CE4E5B9UL
                    + (ulong)stream * 0x94D049BB133111EBUL;

            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;

            // The top 53 bits, which is exactly the precision a double can hold.
            return (x >> 11) * (1.0 / (1UL << 53));
        }
    }
}

/// <summary>The orders this process has accepted. Lost on restart, which is fine.</summary>
internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<string, SupplierOrder> _orders = new(StringComparer.Ordinal);

    private int _nextNumber;

    public SupplierOrder Add(SupplierOrderRequest request, DateTimeOffset receivedAt)
    {
        var orderId = $"SUP-{Interlocked.Increment(ref _nextNumber):000000}";

        var order = new SupplierOrder(
            orderId,
            request.WorkOrderId,
            "Accepted",
            receivedAt,
            [.. request.Lines]);

        _orders[orderId] = order;
        return order;
    }

    public SupplierOrder? Find(string orderId) =>
        _orders.TryGetValue(orderId, out var order) ? order : null;
}

internal sealed record SupplierOrderLine(string PartNumber, int Quantity);

internal sealed record SupplierOrderRequest(Guid WorkOrderId, IReadOnlyList<SupplierOrderLine> Lines);

internal sealed record SupplierOrder(
    string OrderId,
    Guid WorkOrderId,
    string Status,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<SupplierOrderLine> Lines);

namespace Fleet.Common.Time;

/// <summary>
/// The current time, as an injected dependency.
/// </summary>
/// <remarks>
/// Nothing in this repository calls <c>DateTimeOffset.UtcNow</c> inside a domain rule. Certificate
/// expiry, booking windows and outbox retries all read the clock through this interface, so a unit
/// test can say "it is now 2026-03-01" without waiting for it to be true.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Today's date in UTC. Convenient for the many rules expressed in whole days.</summary>
    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

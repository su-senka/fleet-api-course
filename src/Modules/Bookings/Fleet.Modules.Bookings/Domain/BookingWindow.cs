namespace Fleet.Modules.Bookings.Domain;

/// <summary>
/// A half-open interval of time: <c>[StartsAt, EndsAt)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Half-open is the whole trick. The start is inside the interval and the end is not, so a booking
/// that ends at 12:00 and one that starts at 12:00 sit next to each other without touching. Use
/// closed intervals and every handover becomes a conflict; use open ones and a zero-length booking
/// overlaps nothing, including itself.
/// </para>
/// <para>
/// This matches the Postgres range type the exclusion constraint uses -
/// <c>tstzrange(starts_at, ends_at, '[)')</c> - deliberately. The C# rule and the SQL rule have to
/// agree, or the database will reject rows the service was happy with, which is a confusing bug to
/// meet for the first time in production.
/// </para>
/// </remarks>
internal readonly record struct BookingWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt)
{
    public TimeSpan Duration => EndsAt - StartsAt;

    public bool IsEmpty => EndsAt <= StartsAt;

    /// <summary>
    /// Whether the two windows share at least one instant.
    /// </summary>
    /// <remarks>
    /// The classic formulation, and worth reading twice: two intervals overlap when each starts
    /// before the other ends. Both comparisons are strict, which is what makes touching windows
    /// count as separate.
    /// </remarks>
    public bool Overlaps(BookingWindow other) =>
        StartsAt < other.EndsAt && other.StartsAt < EndsAt;

    public override string ToString() => $"[{StartsAt:O}, {EndsAt:O})";
}

namespace Fleet.Modules.Bookings.Contracts;

/// <summary>One booked window, stripped of everything a report does not need.</summary>
public sealed record BookedPeriod(Guid VehicleId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>
/// Read-only access to the calendar, for other modules.
/// </summary>
/// <remarks>
/// Added for Reporting, which computes fleet utilisation and therefore needs to know how long each
/// vehicle was booked for. It gets <see cref="BookedPeriod"/> rather than <see cref="BookingDto"/>:
/// a utilisation report has no business knowing who was driving or why, and a narrower contract is
/// a smaller thing to keep compatible.
/// </remarks>
public interface IBookingCalendar
{
    /// <summary>
    /// Every booking overlapping the window, excluding cancelled ones.
    /// </summary>
    /// <remarks>
    /// Bookings that start before <paramref name="from"/> or end after <paramref name="to"/> are
    /// included, clipped by nothing - the caller decides how to apportion a booking that straddles
    /// a month boundary, because that is a reporting decision rather than a calendar one.
    /// </remarks>
    Task<IReadOnlyList<BookedPeriod>> GetBookedPeriodsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

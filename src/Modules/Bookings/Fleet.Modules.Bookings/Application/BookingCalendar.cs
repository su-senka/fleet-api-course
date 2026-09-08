using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Bookings.Application;

/// <summary>What other modules are allowed to know about the calendar.</summary>
internal sealed class BookingCalendar(BookingsDbContext dbContext) : IBookingCalendar
{
    public async Task<IReadOnlyList<BookedPeriod>> GetBookedPeriodsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status != BookingStatus.Cancelled
                // The same overlap test as everywhere else: a booking counts if any part of it
                // falls inside the window, not only if it starts there.
                && booking.StartsAt < to
                && booking.EndsAt > from)
            .Select(booking => new BookedPeriod(booking.VehicleId, booking.StartsAt, booking.EndsAt))
            .ToListAsync(cancellationToken);
    }
}

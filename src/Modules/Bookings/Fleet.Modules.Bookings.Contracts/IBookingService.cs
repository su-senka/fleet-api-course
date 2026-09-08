using Fleet.Common.Paging;
using Fleet.Common.Results;

namespace Fleet.Modules.Bookings.Contracts;

/// <summary>
/// The application service behind the Bookings endpoints.
/// </summary>
/// <remarks>
/// <para>
/// The richest of the finished services, and the one the reference slice puts endpoints over.
/// It returns every <see cref="ErrorKind"/> the shared kernel defines except
/// <see cref="ErrorKind.Unavailable"/>, which makes it a good place to think hard about status
/// codes:
/// </para>
/// <list type="bullet">
/// <item><description>an unknown vehicle or driver is <see cref="ErrorKind.NotFound"/> - but of
/// <em>what</em>? The booking you are creating does not exist yet.</description></item>
/// <item><description>an overlapping window, an unbookable vehicle and a stale
/// <c>RowVersion</c> are all <see cref="ErrorKind.Conflict"/>, and a client can tell them apart
/// only by the error code. That is what the <c>type</c> field of RFC 9457 is for.</description></item>
/// <item><description>a driver with no valid licence is <see cref="ErrorKind.Conflict"/> rather
/// than <see cref="ErrorKind.Forbidden"/>: the caller is allowed to make the request, the world
/// is simply not in a state where it can succeed.</description></item>
/// </list>
/// </remarks>
public interface IBookingService
{
    /// <summary>
    /// One page of bookings.
    /// </summary>
    /// <param name="filter">
    /// Understands <c>vehicleId</c>, <c>driverId</c>, <c>status</c>, and <c>from</c>/<c>to</c> as
    /// ISO-8601 instants bounding the booking window. The free-text
    /// <see cref="FilterRequest.Search"/> matches the purpose.
    /// <para>
    /// The <c>driverId</c> term is how "a driver sees only their own bookings" gets enforced: the
    /// authorization layer pins it to the caller's own driver id rather than trusting the query
    /// string. Filtering after the fact, in memory, would leak the total count.
    /// </para>
    /// </param>
    /// <param name="sort">Understands <c>startsAt</c>, <c>endsAt</c> and <c>createdAt</c>.</param>
    Task<Result<PagedResult<BookingDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDto>> GetAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books a vehicle.
    /// </summary>
    /// <remarks>
    /// Fails with <see cref="ErrorKind.NotFound"/> for an unknown vehicle or driver, and with
    /// <see cref="ErrorKind.Conflict"/> when the vehicle is not bookable, the driver holds no
    /// valid licence for the whole window, or the window overlaps an existing booking.
    /// </remarks>
    Task<Result<BookingDto>> BookAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a confirmed booking to a new window. Same conflict rules as booking one.</summary>
    Task<Result<BookingDto>> RescheduleAsync(
        Guid bookingId,
        RescheduleBookingCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a booking and frees its window.
    /// </summary>
    /// <remarks>
    /// Cancelling an already-cancelled booking is a <see cref="ErrorKind.Conflict"/> here, because
    /// the domain models a state machine and that transition does not exist. Your endpoint may
    /// still choose to answer an idempotent <c>204</c> - deleting something twice is not usually
    /// an error over HTTP. That mapping is yours to make, which is exactly why the service does
    /// not make it for you.
    /// </remarks>
    Task<Result<BookingDto>> CancelAsync(
        Guid bookingId,
        CancelBookingCommand command,
        CancellationToken cancellationToken = default);
}

using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Common.Time;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Domain;
using Fleet.Modules.Bookings.Persistence;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Vehicles.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fleet.Modules.Bookings.Application;

/// <summary>
/// The Bookings application service.
/// </summary>
/// <remarks>
/// Worth reading for two things. First, how it talks to other modules: through
/// <see cref="IVehicleCatalog"/> and friends, never by querying their tables. Second, how it
/// enforces the overlap rule twice - once in <see cref="BookAsync"/> so the caller gets a decent
/// error, and once in Postgres so that being right about it does not depend on getting the
/// concurrency right.
/// </remarks>
internal sealed class BookingService(
    BookingsDbContext dbContext,
    IVehicleCatalog vehicleCatalog,
    IVehicleAvailability vehicleAvailability,
    IDriverDirectory driverDirectory,
    IDriverEligibility driverEligibility,
    IClock clock) : IBookingService
{
    public async Task<Result<PagedResult<BookingDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        var filtered = dbContext.Bookings.AsNoTracking().ApplyFilter(filter);
        if (filtered.IsFailure)
        {
            return filtered.Error;
        }

        var sorted = filtered.Value.ApplySort(sort);
        if (sorted.IsFailure)
        {
            return sorted.Error;
        }

        var totalCount = await filtered.Value.LongCountAsync(cancellationToken);

        var bookings = await sorted.Value
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        var items = await ToDtosAsync(bookings, cancellationToken);

        return new PagedResult<BookingDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<BookingDto>> GetAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return NotFound(bookingId);
        }

        var dtos = await ToDtosAsync([booking], cancellationToken);
        return dtos[0];
    }

    public async Task<Result<BookingDto>> BookAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = Booking.Create(
            Guid.CreateVersion7(),
            command.VehicleId,
            command.DriverId,
            command.Purpose,
            command.StartsAt,
            command.EndsAt,
            clock.UtcNow);

        if (created.IsFailure)
        {
            return created.Error;
        }

        var booking = created.Value;

        var participants = await CheckParticipantsAsync(booking, cancellationToken);
        if (participants.IsFailure)
        {
            return participants.Error;
        }

        var free = await CheckWindowIsFreeAsync(booking.VehicleId, booking.Window, null, cancellationToken);
        if (free.IsFailure)
        {
            return free.Error;
        }

        dbContext.Bookings.Add(booking);

        var saved = await SaveAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        var dtos = await ToDtosAsync([booking], cancellationToken);
        return dtos[0];
    }

    public async Task<Result<BookingDto>> RescheduleAsync(
        Guid bookingId,
        RescheduleBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking is null)
        {
            return NotFound(bookingId);
        }

        var precondition = CheckExpectedRowVersion(booking, command.ExpectedRowVersion);
        if (precondition.IsFailure)
        {
            return precondition.Error;
        }

        var rescheduled = booking.Reschedule(command.StartsAt, command.EndsAt);
        if (rescheduled.IsFailure)
        {
            return rescheduled.Error;
        }

        // The driver's licence has to cover the new window too. A booking moved six months out is
        // a new question about eligibility, not the same one answered earlier.
        var eligible = await CheckDriverEligibilityAsync(booking, cancellationToken);
        if (eligible.IsFailure)
        {
            return eligible.Error;
        }

        var free = await CheckWindowIsFreeAsync(
            booking.VehicleId, booking.Window, booking.Id, cancellationToken);

        if (free.IsFailure)
        {
            return free.Error;
        }

        var saved = await SaveAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        var dtos = await ToDtosAsync([booking], cancellationToken);
        return dtos[0];
    }

    public async Task<Result<BookingDto>> CancelAsync(
        Guid bookingId,
        CancelBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking is null)
        {
            return NotFound(bookingId);
        }

        var precondition = CheckExpectedRowVersion(booking, command.ExpectedRowVersion);
        if (precondition.IsFailure)
        {
            return precondition.Error;
        }

        var cancelled = booking.Cancel(clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        var saved = await SaveAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        var dtos = await ToDtosAsync([booking], cancellationToken);
        return dtos[0];
    }

    /// <summary>Confirms the vehicle and driver exist, are usable, and may be paired up.</summary>
    private async Task<Result> CheckParticipantsAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (!await vehicleCatalog.ExistsAsync(booking.VehicleId, cancellationToken))
        {
            return Error.NotFound(
                "vehicle.not_found", $"There is no vehicle with id {booking.VehicleId}.");
        }

        if (!await driverDirectory.ExistsAsync(booking.DriverId, cancellationToken))
        {
            return Error.NotFound(
                "driver.not_found", $"There is no driver with id {booking.DriverId}.");
        }

        // Asked as a separate question from "does it exist", so that a vehicle in the workshop is
        // a 409 rather than a 404. Bookings does not read the vehicle's status and decide for
        // itself - the rule belongs to Vehicles, and only the answer crosses the boundary.
        if (!await vehicleAvailability.IsBookableAsync(booking.VehicleId, cancellationToken))
        {
            return Error.Conflict(
                "vehicle.not_bookable",
                "That vehicle is not available for booking. It may be in maintenance or retired.");
        }

        return await CheckDriverEligibilityAsync(booking, cancellationToken);
    }

    private async Task<Result> CheckDriverEligibilityAsync(Booking booking, CancellationToken cancellationToken)
    {
        var firstDay = DateOnly.FromDateTime(booking.StartsAt.UtcDateTime);
        var lastDay = DateOnly.FromDateTime(booking.EndsAt.UtcDateTime);

        // Both ends of the window, because a licence that expires mid-trip leaves the driver
        // unlicensed for the second half of it. Checking only the start date is the easy mistake.
        if (!await driverEligibility.CanDriveAsync(booking.DriverId, firstDay, cancellationToken))
        {
            return Error.Conflict(
                "driver.not_eligible",
                "That driver has no valid licence for the start of the booking.");
        }

        if (lastDay != firstDay
            && !await driverEligibility.CanDriveAsync(booking.DriverId, lastDay, cancellationToken))
        {
            return Error.Conflict(
                "driver.licence_expires_during_booking",
                "That driver's licence expires before the booking ends.");
        }

        return Result.Success();
    }

    /// <summary>
    /// The first line of defence against a double booking.
    /// </summary>
    /// <remarks>
    /// This exists to produce a good error message, not to guarantee correctness. Between this
    /// query and the INSERT that follows, another transaction can commit a conflicting booking -
    /// and under concurrency it will. The exclusion constraint in Postgres is what actually makes
    /// the rule true; see <see cref="SaveAsync"/>, which turns its violation into the same error
    /// this method returns.
    /// </remarks>
    private async Task<Result> CheckWindowIsFreeAsync(
        Guid vehicleId,
        BookingWindow window,
        Guid? excludingBookingId,
        CancellationToken cancellationToken)
    {
        var clashes = await dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.VehicleId == vehicleId
                && booking.Status != BookingStatus.Cancelled
                && (excludingBookingId == null || booking.Id != excludingBookingId)
                // The same strict comparisons as BookingWindow.Overlaps, so that touching
                // windows are allowed here exactly as they are in the domain and in SQL.
                && booking.StartsAt < window.EndsAt
                && window.StartsAt < booking.EndsAt)
            .Select(booking => new { booking.Id, booking.StartsAt, booking.EndsAt })
            .FirstOrDefaultAsync(cancellationToken);

        return clashes is null
            ? Result.Success()
            : Error.Conflict(
                "booking.overlaps_existing",
                $"That vehicle is already booked from {clashes.StartsAt:u} to {clashes.EndsAt:u}.");
    }

    /// <summary>
    /// Saves, translating the two database-level failures into ordinary errors.
    /// </summary>
    /// <remarks>
    /// Both of these are expected outcomes under concurrency, not faults, so neither is allowed to
    /// escape as an exception. This is the seam where "the database said no" becomes "409".
    /// </remarks>
    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
            when (PostgresErrorCodeOf(exception) == PostgresErrorCodes.ExclusionViolation)
        {
            // The exclusion constraint caught a booking that our own check said was fine, which
            // means another transaction committed in between. Exactly the race the constraint is
            // there for, and the caller gets the same 409 either way.
            return Error.Conflict(
                "booking.overlaps_existing",
                "That vehicle was booked for an overlapping window while this request was in flight.");
        }
        catch (Exception exception)
            when (PostgresErrorCodeOf(exception)
                  is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)
        {
            // Two transactions inserting overlapping windows for the same vehicle can deadlock
            // instead of producing a clean exclusion violation: each takes a speculative lock on
            // its own index entry and then waits on the other's, and Postgres breaks the cycle by
            // killing one of them.
            //
            // Not a bug and not a 500. From the caller's point of view it is the same situation as
            // losing the race - somebody else is writing to this vehicle's calendar - and the same
            // answer applies: try again. A separate code, so a client can tell "you definitely
            // clash" from "we could not tell, retry".
            return Error.Conflict(
                "booking.write_conflict",
                "Another request was writing to this vehicle's calendar at the same time. Try again.");
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody updated the row between our read and our write. Their change stands; the
            // caller re-reads and decides what to do about it.
            return Error.Conflict(
                "booking.version_mismatch",
                "This booking has changed since you last read it. Fetch it again and retry.");
        }
    }

    /// <summary>
    /// Finds the Postgres error code anywhere in an exception chain, or <c>null</c> if there is none.
    /// </summary>
    /// <remarks>
    /// Matching on <c>DbUpdateException</c> with a <c>PostgresException</c> inner is the obvious
    /// thing to write, and it is not enough. EF Core wraps a transient failure in an
    /// <c>InvalidOperationException</c> - "likely due to a transient failure" - when no retrying
    /// execution strategy is configured, so the same deadlock arrives as one type or the other
    /// depending on where it was detected. Walking the chain is the only reliable way to ask
    /// "what did Postgres actually say?".
    ///
    /// A ten-way parallel booking test found this. Two concurrent requests almost never do.
    /// </remarks>
    private static string? PostgresErrorCodeOf(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres.SqlState;
            }
        }

        return null;
    }

    private static Result CheckExpectedRowVersion(Booking booking, string? expectedRowVersion)
    {
        if (expectedRowVersion is null)
        {
            // No precondition offered, so none is enforced. The endpoint decides whether to demand
            // an If-Match header; the service will not invent the requirement.
            return Result.Success();
        }

        if (!RowVersionToken.TryDecode(expectedRowVersion, out var expected))
        {
            return Error.Conflict(
                "booking.version_malformed",
                "The supplied version is not one this API issued.");
        }

        return expected == booking.RowVersion
            ? Result.Success()
            : Error.Conflict(
                "booking.version_mismatch",
                "This booking has changed since you last read it. Fetch it again and retry.");
    }

    /// <summary>
    /// Turns bookings into DTOs, resolving vehicles and drivers in one call each.
    /// </summary>
    /// <remarks>
    /// This is where the N+1 problem would live if it were going to. A page of 20 bookings needs
    /// at most 20 vehicles and 20 drivers; asking for them one at a time would be 40 round trips
    /// across two modules. <c>GetManyAsync</c> makes it two.
    /// </remarks>
    private async Task<IReadOnlyList<BookingDto>> ToDtosAsync(
        IReadOnlyList<Booking> bookings,
        CancellationToken cancellationToken)
    {
        if (bookings.Count == 0)
        {
            return [];
        }

        var vehicles = await vehicleCatalog.GetManyAsync(
            [.. bookings.Select(booking => booking.VehicleId)], cancellationToken);

        var drivers = await driverDirectory.GetManyAsync(
            [.. bookings.Select(booking => booking.DriverId)], cancellationToken);

        return
        [
            .. bookings.Select(booking => new BookingDto(
                booking.Id,
                booking.VehicleId,
                vehicles.TryGetValue(booking.VehicleId, out var vehicle) ? vehicle.Plate : UnknownLabel,
                booking.DriverId,
                drivers.TryGetValue(booking.DriverId, out var driver) ? driver.Name : UnknownLabel,
                booking.Purpose,
                booking.StartsAt,
                booking.EndsAt,
                booking.Status,
                booking.CreatedAt,
                booking.CancelledAt,
                RowVersionToken.Encode(booking.RowVersion)))
        ];
    }

    /// <summary>
    /// Shown when the referenced row has gone from the other module's schema.
    /// </summary>
    /// <remarks>
    /// It can happen, precisely because there is no foreign key stopping it. That is the cost of
    /// schema-per-module, and pretending otherwise by throwing here would turn a cosmetic gap in
    /// one row into a failed request for the whole page.
    /// </remarks>
    private const string UnknownLabel = "(unknown)";

    private static Error NotFound(Guid bookingId) =>
        Error.NotFound("booking.not_found", $"There is no booking with id {bookingId}.");
}

using Fleet.Common.Results;
using Fleet.Modules.Bookings.Contracts;

namespace Fleet.Modules.Bookings.Domain;

/// <summary>
/// A vehicle held for a driver over a window of time.
/// </summary>
/// <remarks>
/// The rule that matters - one vehicle cannot be in two places at once - is not enforced here.
/// It cannot be: an entity can only see itself, and the question is about every <em>other</em>
/// booking for the same vehicle. The service asks that question, and the database answers it
/// again with an exclusion constraint. See <c>BookingService.BookAsync</c>.
/// </remarks>
internal sealed class Booking
{
    private Booking() => Purpose = string.Empty;

    private Booking(
        Guid id,
        Guid vehicleId,
        Guid driverId,
        string purpose,
        BookingWindow window,
        DateTimeOffset createdAt)
    {
        Id = id;
        VehicleId = vehicleId;
        DriverId = driverId;
        Purpose = purpose;
        StartsAt = window.StartsAt;
        EndsAt = window.EndsAt;
        Status = BookingStatus.Confirmed;
        CreatedAt = createdAt;
    }

    public const int PurposeMaxLength = 200;

    /// <summary>The shortest booking worth making. Below this it is a typo, not a trip.</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(15);

    /// <summary>A ceiling, so that a mistyped year cannot block a vehicle until 3025.</summary>
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromDays(90);

    public Guid Id { get; private set; }

    /// <summary>
    /// The vehicle, as a plain <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// There is no foreign key behind this and no navigation property to follow. The vehicle lives
    /// in another module's schema, and this module confirms it exists by asking
    /// <c>IVehicleCatalog</c> - not by asking Postgres.
    /// </remarks>
    public Guid VehicleId { get; private set; }

    public Guid DriverId { get; private set; }

    public string Purpose { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public BookingStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Postgres' <c>xmin</c> system column: the id of the transaction that last wrote this row.
    /// </summary>
    /// <remarks>
    /// Every table already has it, so optimistic concurrency costs no extra column, no trigger and
    /// no application code to bump a counter. EF Core reads it as a concurrency token; if the value
    /// in the UPDATE's WHERE clause no longer matches, zero rows are affected and EF raises
    /// <c>DbUpdateConcurrencyException</c>.
    /// </remarks>
    public uint RowVersion { get; private set; }

    public BookingWindow Window => new(StartsAt, EndsAt);

    /// <summary>
    /// Whether this booking holds the vehicle. Cancelled bookings do not, which is why the
    /// exclusion constraint is a partial one.
    /// </summary>
    public bool BlocksVehicle => Status != BookingStatus.Cancelled;

    public static Result<Booking> Create(
        Guid id,
        Guid vehicleId,
        Guid driverId,
        string purpose,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset createdAt)
    {
        var trimmedPurpose = purpose?.Trim() ?? string.Empty;

        if (trimmedPurpose.Length == 0)
        {
            return Error.Validation("booking.purpose_required", "A booking needs a purpose.");
        }

        if (trimmedPurpose.Length > PurposeMaxLength)
        {
            return Error.Validation(
                "booking.purpose_too_long",
                $"A purpose is at most {PurposeMaxLength} characters.");
        }

        var window = new BookingWindow(startsAt.ToUniversalTime(), endsAt.ToUniversalTime());

        var windowIsValid = ValidateWindow(window);
        if (windowIsValid.IsFailure)
        {
            return windowIsValid.Error;
        }

        return new Booking(id, vehicleId, driverId, trimmedPurpose, window, createdAt);
    }

    /// <summary>
    /// Moves the booking. Only a confirmed booking can move; a completed trip is history and a
    /// cancelled one has already given up its slot.
    /// </summary>
    public Result Reschedule(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Error.Conflict(
                "booking.not_confirmed",
                $"A {Status.ToString().ToLowerInvariant()} booking cannot be rescheduled.");
        }

        var window = new BookingWindow(startsAt.ToUniversalTime(), endsAt.ToUniversalTime());

        var windowIsValid = ValidateWindow(window);
        if (windowIsValid.IsFailure)
        {
            return windowIsValid.Error;
        }

        StartsAt = window.StartsAt;
        EndsAt = window.EndsAt;

        return Result.Success();
    }

    public Result Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == BookingStatus.Cancelled)
        {
            return Error.Conflict("booking.already_cancelled", "This booking is already cancelled.");
        }

        if (Status == BookingStatus.Completed)
        {
            return Error.Conflict("booking.already_completed", "A completed booking cannot be cancelled.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAt = cancelledAt;

        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Error.Conflict(
                "booking.not_confirmed",
                $"A {Status.ToString().ToLowerInvariant()} booking cannot be completed.");
        }

        Status = BookingStatus.Completed;
        return Result.Success();
    }

    private static Result ValidateWindow(BookingWindow window)
    {
        if (window.IsEmpty)
        {
            return Error.Validation(
                "booking.ends_before_it_starts",
                "A booking must end after it starts.");
        }

        if (window.Duration < MinimumDuration)
        {
            return Error.Validation(
                "booking.too_short",
                $"A booking must last at least {MinimumDuration.TotalMinutes:0} minutes.");
        }

        if (window.Duration > MaximumDuration)
        {
            return Error.Validation(
                "booking.too_long",
                $"A booking cannot last more than {MaximumDuration.TotalDays:0} days.");
        }

        return Result.Success();
    }
}

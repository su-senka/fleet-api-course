namespace Fleet.Modules.Bookings.Contracts;

/// <summary>
/// Books a vehicle for a driver over a window of time.
/// </summary>
/// <remarks>
/// The window is half-open: <c>[StartsAt, EndsAt)</c>. A booking ending at 12:00 and one starting
/// at 12:00 do not overlap, which is the behaviour everybody expects and almost nobody implements
/// correctly on the first attempt.
/// </remarks>
public sealed record CreateBookingCommand(
    Guid VehicleId,
    Guid DriverId,
    string Purpose,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

/// <summary>
/// Moves an existing booking to a new window.
/// </summary>
/// <param name="ExpectedRowVersion">
/// The <c>RowVersion</c> the caller last saw, or <c>null</c> to update regardless.
/// <para>
/// Passing it makes the write conditional: if anyone has touched the booking since, the service
/// returns <see cref="Common.Results.ErrorKind.Conflict"/> instead of quietly overwriting them.
/// Whether your endpoint <em>requires</em> an <c>If-Match</c> header, or merely honours one, is a
/// design decision - and a good one to argue about before you make it.
/// </para>
/// </param>
public sealed record RescheduleBookingCommand(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? ExpectedRowVersion = null);

/// <summary>Calls a booking off, freeing the vehicle for that window.</summary>
public sealed record CancelBookingCommand(string? ExpectedRowVersion = null);

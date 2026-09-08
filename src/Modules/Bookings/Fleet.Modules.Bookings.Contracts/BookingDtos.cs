namespace Fleet.Modules.Bookings.Contracts;

/// <summary>
/// A booking, with the vehicle and driver named rather than merely referenced.
/// </summary>
/// <param name="VehiclePlate">
/// Resolved through <c>IVehicleCatalog</c>, not by joining to the <c>vehicles</c> schema - there
/// is no foreign key between the two, and no query crosses the boundary.
/// </param>
/// <param name="RowVersion">
/// An opaque token identifying this exact revision of the booking. Base64 of the row's Postgres
/// <c>xmin</c>, which changes on every update.
/// <para>
/// This is what an <c>ETag</c> is built from. Send it back in <c>If-Match</c> on an update and the
/// service will refuse the write if somebody else has changed the booking in the meantime. Treat
/// it as opaque: its format is not part of the contract and comparing it for anything other than
/// equality is a mistake.
/// </para>
/// </param>
public sealed record BookingDto(
    Guid Id,
    Guid VehicleId,
    string VehiclePlate,
    Guid DriverId,
    string DriverName,
    string Purpose,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string RowVersion);

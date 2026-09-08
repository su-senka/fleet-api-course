namespace Fleet.Modules.Vehicles.Contracts;

/// <summary>A depot: where vehicles live when nobody has booked them.</summary>
public sealed record DepotDto(Guid Id, string Name, string City);

/// <summary>
/// A vehicle as it appears in a list.
/// </summary>
/// <remarks>
/// Note what is not here: no navigation properties, no odometer history, no depot object. A list
/// endpoint that returned those would issue one query per row - the N+1 problem - and with 250
/// vehicles in the seed data you would see it in Jaeger straight away.
/// </remarks>
public sealed record VehicleDto(
    Guid Id,
    string Plate,
    VehicleType Type,
    VehicleStatus Status,
    int OdometerKm,
    Guid DepotId,
    string DepotName);

/// <summary>A single vehicle, with the depot expanded. Returned when fetching one by id.</summary>
public sealed record VehicleDetailDto(
    Guid Id,
    string Plate,
    VehicleType Type,
    VehicleStatus Status,
    int OdometerKm,
    DepotDto Depot,
    DateTimeOffset? LastOdometerReadingAt);

/// <summary>One odometer reading from a vehicle's history.</summary>
public sealed record OdometerReadingDto(Guid Id, Guid VehicleId, DateTimeOffset RecordedAt, int Km);

/// <summary>
/// The narrow view of a vehicle that other modules are allowed to see.
/// </summary>
/// <remarks>
/// Deliberately smaller than <see cref="VehicleDto"/>. Bookings needs to know that a vehicle
/// exists and roughly what it is; it has no business knowing its mileage.
/// </remarks>
public sealed record VehicleSummary(Guid Id, string Plate, VehicleType Type, VehicleStatus Status);

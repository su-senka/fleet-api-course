namespace Fleet.Modules.Vehicles.Contracts;

/// <summary>
/// Adds a vehicle to the fleet. New vehicles start <see cref="VehicleStatus.Available"/>.
/// </summary>
/// <remarks>
/// A command is not a request body. The endpoint you write takes whatever shape suits your API,
/// validates it, and maps it to this. Keeping the two apart is what stops a change to the wire
/// format from rippling into the domain.
/// </remarks>
public sealed record RegisterVehicleCommand(
    string Plate,
    VehicleType Type,
    Guid DepotId,
    int OdometerKm);

/// <summary>Records a new odometer reading. Readings only ever go up.</summary>
public sealed record RecordOdometerCommand(DateTimeOffset RecordedAt, int Km);

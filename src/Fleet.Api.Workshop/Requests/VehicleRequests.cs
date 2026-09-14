using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Api.Workshop.Requests;

public sealed record RegisterVehicleRequest(
    string Plate,
    VehicleType Type,
    Guid DepotId,
    int OdometerKm);

public sealed record ChangeVehicleStatusRequest(VehicleStatus Status);

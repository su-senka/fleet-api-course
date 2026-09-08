using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Api.Requests;

/// <summary>
/// The body of <c>POST /vehicles</c>.
/// </summary>
/// <remarks>
/// A wire shape, not a domain command. It exists separately from
/// <see cref="RegisterVehicleCommand"/> so that the JSON a client sends can change - a renamed
/// field, a deprecated one kept for a release - without any of that reaching the module. The two
/// look almost identical today, which is exactly when people are tempted to collapse them, and
/// exactly when it is cheapest not to.
/// </remarks>
public sealed record RegisterVehicleRequest(
    string Plate,
    VehicleType Type,
    Guid DepotId,
    int OdometerKm);

/// <summary>The body of <c>PUT /vehicles/{id}/status</c>.</summary>
public sealed record ChangeVehicleStatusRequest(VehicleStatus Status);

/// <summary>The body of <c>POST /vehicles/{id}/odometer-readings</c>.</summary>
public sealed record RecordOdometerRequest(DateTimeOffset RecordedAt, int Km);

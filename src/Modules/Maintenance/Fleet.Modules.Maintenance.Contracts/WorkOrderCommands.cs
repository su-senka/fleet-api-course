namespace Fleet.Modules.Maintenance.Contracts;

/// <summary>Raises a work order against a vehicle.</summary>
public sealed record OpenWorkOrderCommand(Guid VehicleId, WorkOrderKind Kind);

/// <summary>
/// Adds parts to a work order, or increases the quantity if the part is already on it.
/// </summary>
public sealed record AddPartLineCommand(string PartNumber, int Quantity, decimal UnitPrice);

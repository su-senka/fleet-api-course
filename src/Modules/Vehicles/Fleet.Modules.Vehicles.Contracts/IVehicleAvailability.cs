namespace Fleet.Modules.Vehicles.Contracts;

/// <summary>
/// Answers whether a vehicle may be booked at all.
/// </summary>
/// <remarks>
/// Separate from <see cref="IVehicleCatalog"/> because it answers a different kind of question.
/// The catalog says what a vehicle <em>is</em>; this says what the Vehicles module will
/// <em>allow</em>. Bookings calls it and does not care that the current rule is "status must be
/// Available" - when a second condition is added, no caller changes.
/// </remarks>
public interface IVehicleAvailability
{
    /// <summary>
    /// <c>true</c> when the vehicle exists and its status permits booking. A vehicle that does
    /// not exist is not bookable either, so check <see cref="IVehicleCatalog.ExistsAsync"/> first
    /// if you need to tell a 404 from a 409.
    /// </summary>
    Task<bool> IsBookableAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}

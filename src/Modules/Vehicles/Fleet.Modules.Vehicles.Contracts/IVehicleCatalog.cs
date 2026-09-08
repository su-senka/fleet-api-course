namespace Fleet.Modules.Vehicles.Contracts;

/// <summary>
/// Read-only lookup of vehicles, for other modules.
/// </summary>
/// <remarks>
/// This is the whole reason Bookings can store a <c>VehicleId</c> as a plain <see cref="Guid"/>
/// with no foreign key pointing at the <c>vehicles</c> schema. Instead of the database enforcing
/// that the vehicle exists, Bookings asks - and gets an ordinary answer it can turn into a 404.
/// </remarks>
public interface IVehicleCatalog
{
    /// <summary>Cheap existence check. Does not load the vehicle.</summary>
    Task<bool> ExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>The vehicle, or <c>null</c> when there is no such id.</summary>
    Task<VehicleSummary?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The vehicles matching the given ids, in one query.
    /// </summary>
    /// <remarks>
    /// Exists so that a caller listing 50 bookings can resolve 50 vehicles with one round trip
    /// instead of 50 calls to <see cref="GetAsync"/>. Unknown ids are simply absent from the
    /// result.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, VehicleSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> vehicleIds,
        CancellationToken cancellationToken = default);
}

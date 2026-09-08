namespace Fleet.Modules.Drivers.Contracts;

/// <summary>
/// Read-only lookup of drivers, for other modules.
/// </summary>
/// <remarks>
/// The Drivers counterpart of <c>IVehicleCatalog</c>. Bookings stores a <c>DriverId</c> as a plain
/// <see cref="Guid"/> and asks here whether it means anything.
/// </remarks>
public interface IDriverDirectory
{
    Task<bool> ExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<DriverSummary?> GetAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>Resolves the driver, in one query, for each id that exists.</summary>
    Task<IReadOnlyDictionary<Guid, DriverSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the driver who signs in as <paramref name="userId"/>, or <c>null</c> when the signed-in
    /// user is not a driver.
    /// </summary>
    /// <remarks>
    /// This is how "a driver may read only their own bookings" gets answered. The authorization
    /// handler has a token; it needs a driver id; this is the only bridge between the two, and it
    /// deliberately lives in Drivers rather than in whatever module happens to be asking.
    /// </remarks>
    Task<DriverSummary?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}

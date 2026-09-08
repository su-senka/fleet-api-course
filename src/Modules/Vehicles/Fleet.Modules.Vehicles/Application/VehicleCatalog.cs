using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Vehicles.Application;

/// <summary>
/// What other modules are allowed to know about vehicles.
/// </summary>
/// <remarks>
/// Both cross-module interfaces are implemented by one class because they hit the same table and
/// there is nothing to gain by splitting the implementation. They stay two interfaces because
/// Bookings should be able to depend on "can this be booked" without also being handed a way to
/// read every vehicle in the fleet.
/// </remarks>
internal sealed class VehicleCatalog(VehiclesDbContext dbContext) : IVehicleCatalog, IVehicleAvailability
{
    public Task<bool> ExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        dbContext.Vehicles.AsNoTracking().AnyAsync(vehicle => vehicle.Id == vehicleId, cancellationToken);

    public async Task<VehicleSummary?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId)
            .Select(vehicle => new VehicleSummary(vehicle.Id, vehicle.Plate, vehicle.Type, vehicle.Status))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, VehicleSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> vehicleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vehicleIds);

        if (vehicleIds.Count == 0)
        {
            return new Dictionary<Guid, VehicleSummary>();
        }

        // Distinct first: a page of bookings for the same van would otherwise send the same id
        // twenty times and make Postgres do the de-duplication.
        var ids = vehicleIds.Distinct().ToArray();

        var summaries = await dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => ids.Contains(vehicle.Id))
            .Select(vehicle => new VehicleSummary(vehicle.Id, vehicle.Plate, vehicle.Type, vehicle.Status))
            .ToListAsync(cancellationToken);

        return summaries.ToDictionary(summary => summary.Id);
    }

    public async Task<bool> IsBookableAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        // Projecting the status rather than loading the entity keeps this to one small round trip;
        // the rule itself still lives on Vehicle.IsBookable.
        var status = await dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId)
            .Select(vehicle => (VehicleStatus?)vehicle.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status == VehicleStatus.Available;
    }
}

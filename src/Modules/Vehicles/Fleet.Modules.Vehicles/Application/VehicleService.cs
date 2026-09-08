using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Domain;
using Fleet.Modules.Vehicles.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Vehicles.Application;

/// <summary>
/// The Vehicles application service: finished, tested, and completely unaware of HTTP.
/// </summary>
/// <remarks>
/// Notice how little there is here. Each method loads what it needs, asks the entity to do the
/// work, saves, and maps to a DTO. The rules live on <see cref="Vehicle"/>; this class only
/// arranges the trip to the database and back.
/// </remarks>
internal sealed class VehicleService(VehiclesDbContext dbContext) : IVehicleService
{
    public async Task<Result<PagedResult<VehicleDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        // AsNoTracking because nothing here is going to be modified. Change tracking on a page of
        // 100 rows is wasted work, and it is the single easiest performance win in EF Core.
        var query = dbContext.Vehicles.AsNoTracking().ApplyFilter(filter);

        var sorted = query.ApplySort(sort);
        if (sorted.IsFailure)
        {
            return sorted.Error;
        }

        // One COUNT and one SELECT. The count runs against the filtered query but before paging,
        // which is what makes TotalCount mean "matches" rather than "rows on this page".
        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await sorted.Value
            .Skip(page.Skip)
            .Take(page.Take)
            // Projecting inside the query means Postgres joins depots once and returns 20 rows.
            // Loading entities and mapping afterwards would either need an Include or issue one
            // query per row for the depot name - the N+1 problem, in its natural habitat.
            .Select(vehicle => new VehicleDto(
                vehicle.Id,
                vehicle.Plate,
                vehicle.Type,
                vehicle.Status,
                vehicle.OdometerKm,
                vehicle.DepotId,
                vehicle.Depot!.Name))
            .ToListAsync(cancellationToken);

        return new PagedResult<VehicleDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<VehicleDetailDto>> GetAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .Include(v => v.Depot)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        return vehicle is null ? NotFound(vehicleId) : ToDetail(vehicle);
    }

    public async Task<Result<VehicleDetailDto>> RegisterAsync(
        RegisterVehicleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var depot = await dbContext.Depots
            .FirstOrDefaultAsync(d => d.Id == command.DepotId, cancellationToken);

        if (depot is null)
        {
            return Error.NotFound("depot.not_found", $"There is no depot with id {command.DepotId}.");
        }

        var created = Vehicle.Register(
            Guid.CreateVersion7(),
            command.Plate,
            command.Type,
            command.DepotId,
            command.OdometerKm);

        if (created.IsFailure)
        {
            return created.Error;
        }

        var vehicle = created.Value;

        var plateTaken = await dbContext.Vehicles
            .AnyAsync(v => v.Plate == vehicle.Plate, cancellationToken);

        if (plateTaken)
        {
            return Error.Conflict(
                "vehicle.plate_taken",
                $"Plate {vehicle.Plate} is already registered to another vehicle.");
        }

        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDetail(vehicle, depot);
    }

    public async Task<Result<VehicleDetailDto>> ChangeStatusAsync(
        Guid vehicleId,
        VehicleStatus status,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await dbContext.Vehicles
            .Include(v => v.Depot)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle is null)
        {
            return NotFound(vehicleId);
        }

        var changed = vehicle.ChangeStatus(status);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDetail(vehicle);
    }

    public async Task<Result<OdometerReadingDto>> RecordOdometerAsync(
        Guid vehicleId,
        RecordOdometerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle is null)
        {
            return NotFound(vehicleId);
        }

        var recorded = vehicle.RecordOdometerReading(
            Guid.CreateVersion7(),
            command.RecordedAt.ToUniversalTime(),
            command.Km);

        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var reading = recorded.Value;
        return new OdometerReadingDto(reading.Id, reading.VehicleId, reading.RecordedAt, reading.Km);
    }

    public async Task<Result<PagedResult<OdometerReadingDto>>> ListOdometerReadingsAsync(
        Guid vehicleId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var exists = await dbContext.Vehicles.AnyAsync(v => v.Id == vehicleId, cancellationToken);
        if (!exists)
        {
            return NotFound(vehicleId);
        }

        var query = dbContext.OdometerReadings
            .AsNoTracking()
            .Where(reading => reading.VehicleId == vehicleId);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(reading => reading.RecordedAt)
            .ThenByDescending(reading => reading.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(reading => new OdometerReadingDto(
                reading.Id,
                reading.VehicleId,
                reading.RecordedAt,
                reading.Km))
            .ToListAsync(cancellationToken);

        return new PagedResult<OdometerReadingDto>(items, page.Page, page.PageSize, totalCount);
    }

    private static Error NotFound(Guid vehicleId) =>
        Error.NotFound("vehicle.not_found", $"There is no vehicle with id {vehicleId}.");

    private static VehicleDetailDto ToDetail(Vehicle vehicle, Domain.Depot? depot = null)
    {
        var resolved = depot ?? vehicle.Depot
            ?? throw new InvalidOperationException(
                $"Vehicle {vehicle.Id} was loaded without its depot. Add an Include.");

        return new VehicleDetailDto(
            vehicle.Id,
            vehicle.Plate,
            vehicle.Type,
            vehicle.Status,
            vehicle.OdometerKm,
            new DepotDto(resolved.Id, resolved.Name, resolved.City),
            vehicle.LastReadingAt);
    }
}

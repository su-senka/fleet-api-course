using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Vehicles.Application;

internal sealed class DepotService(VehiclesDbContext dbContext) : IDepotService
{
    public async Task<Result<IReadOnlyList<DepotDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var depots = await dbContext.Depots
            .AsNoTracking()
            .OrderBy(depot => depot.Name)
            .Select(depot => new DepotDto(depot.Id, depot.Name, depot.City))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DepotDto>>.Success(depots);
    }

    public async Task<Result<DepotDto>> GetAsync(Guid depotId, CancellationToken cancellationToken = default)
    {
        var depot = await dbContext.Depots
            .AsNoTracking()
            .Where(d => d.Id == depotId)
            .Select(d => new DepotDto(d.Id, d.Name, d.City))
            .FirstOrDefaultAsync(cancellationToken);

        return depot is null
            ? Error.NotFound("depot.not_found", $"There is no depot with id {depotId}.")
            : depot;
    }
}

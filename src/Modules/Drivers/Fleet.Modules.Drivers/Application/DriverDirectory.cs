using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Drivers.Application;

/// <summary>What other modules are allowed to know about drivers.</summary>
internal sealed class DriverDirectory(DriversDbContext dbContext) : IDriverDirectory, IDriverEligibility
{
    public Task<bool> ExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        dbContext.Drivers.AsNoTracking().AnyAsync(driver => driver.Id == driverId, cancellationToken);

    public async Task<DriverSummary?> GetAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .AsNoTracking()
            .Where(driver => driver.Id == driverId)
            .Select(driver => new DriverSummary(driver.Id, driver.EmployeeNumber, driver.Name))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, DriverSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(driverIds);

        if (driverIds.Count == 0)
        {
            return new Dictionary<Guid, DriverSummary>();
        }

        var ids = driverIds.Distinct().ToArray();

        var summaries = await dbContext.Drivers
            .AsNoTracking()
            .Where(driver => ids.Contains(driver.Id))
            .Select(driver => new DriverSummary(driver.Id, driver.EmployeeNumber, driver.Name))
            .ToListAsync(cancellationToken);

        return summaries.ToDictionary(summary => summary.Id);
    }

    public async Task<DriverSummary?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await dbContext.Drivers
            .AsNoTracking()
            .Where(driver => driver.UserId == userId)
            .Select(driver => new DriverSummary(driver.Id, driver.EmployeeNumber, driver.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> CanDriveAsync(
        Guid driverId,
        DateOnly on,
        CancellationToken cancellationToken = default)
    {
        // Loads the driver with their certificates and asks the domain, rather than restating the
        // rule as a second EXISTS query. A driver holds a handful of certificates, so the extra
        // rows are cheap - and the rule stays in exactly one place, which is worth far more than
        // the round trip it costs.
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .Include(d => d.Certificates)
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        return driver is not null && driver.CanDriveOn(on);
    }
}

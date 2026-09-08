using Fleet.Common.Persistence;
using Fleet.Common.Time;
using Fleet.Modules.Vehicles.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Modules.Vehicles.Persistence;

/// <summary>Migrates and seeds the <c>vehicles</c> schema.</summary>
internal sealed class VehiclesDatabaseInitializer(
    VehiclesDbContext dbContext,
    IClock clock,
    ILogger<VehiclesDatabaseInitializer> logger) : IModuleDatabaseInitializer
{
    public string ModuleName => VehiclesDbContext.Schema;

    /// <summary>First. Drivers and Bookings seed data is generated against these vehicles.</summary>
    public int Order => 10;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Vehicles.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Vehicles schema already seeded, leaving it alone");
            return;
        }

        var depots = VehiclesSeedData.BuildDepots();
        var vehicles = VehiclesSeedData.BuildVehicles(depots, clock.UtcNow);

        dbContext.Depots.AddRange(depots);
        dbContext.Vehicles.AddRange(vehicles);

        // 250 vehicles and 15,000 readings in one SaveChanges. Npgsql batches the inserts, so this
        // is a handful of round trips rather than 15,250 - which is why the seed takes a second
        // and not a coffee break.
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {DepotCount} depots, {VehicleCount} vehicles and {ReadingCount} odometer readings",
            depots.Count,
            vehicles.Count,
            vehicles.Sum(vehicle => vehicle.OdometerReadings.Count));
    }
}

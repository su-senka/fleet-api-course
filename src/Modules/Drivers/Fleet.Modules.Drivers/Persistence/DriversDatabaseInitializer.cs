using Fleet.Common.Persistence;
using Fleet.Common.Time;
using Fleet.Modules.Drivers.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Modules.Drivers.Persistence;

/// <summary>Migrates and seeds the <c>drivers</c> schema.</summary>
internal sealed class DriversDatabaseInitializer(
    DriversDbContext dbContext,
    IClock clock,
    ILogger<DriversDatabaseInitializer> logger) : IModuleDatabaseInitializer
{
    public string ModuleName => DriversDbContext.Schema;

    /// <summary>After Vehicles, before Bookings.</summary>
    public int Order => 20;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Drivers.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Drivers schema already seeded, leaving it alone");
            return;
        }

        var drivers = DriversSeedData.BuildDrivers(clock.UtcNow);

        dbContext.Drivers.AddRange(drivers);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {DriverCount} drivers and {CertificateCount} certificates",
            drivers.Count,
            drivers.Sum(driver => driver.Certificates.Count));
    }
}

using Fleet.Common.Persistence;
using Fleet.Common.Storage;
using Fleet.Common.Time;
using Fleet.Modules.Drivers.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Modules.Drivers.Persistence;

/// <summary>Migrates and seeds the <c>drivers</c> schema.</summary>
internal sealed class DriversDatabaseInitializer(
    DriversDbContext dbContext,
    IClock clock,
    IBlobStore blobStore,
    ILogger<DriversDatabaseInitializer> logger) : IModuleDatabaseInitializer
{
    private const string CertificatesContainer = "certificates";
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

        var seed = DriversSeedData.BuildDrivers(clock.UtcNow);

        foreach (var scan in seed.Scans)
        {
            var content = PlaceholderPdf.Build($"{scan.Certificate.Kind} scan - {scan.FileName}");
            await using var stream = new MemoryStream(content);

            var blobId = await blobStore.SaveAsync(
                CertificatesContainer,
                scan.FileName,
                stream,
                "application/pdf",
                cancellationToken);

            scan.Certificate.AttachScan(blobId);
        }

        dbContext.Drivers.AddRange(seed.Drivers);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {DriverCount} drivers, {CertificateCount} certificates, and {ScanCount} certificate scans",
            seed.Drivers.Count,
            seed.Drivers.Sum(driver => driver.Certificates.Count),
            seed.Scans.Count);
    }
}

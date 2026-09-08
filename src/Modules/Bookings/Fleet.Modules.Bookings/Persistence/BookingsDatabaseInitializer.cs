using Fleet.Common.Persistence;
using Fleet.Common.Time;
using Fleet.Modules.Bookings.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Modules.Bookings.Persistence;

/// <summary>Migrates and seeds the <c>bookings</c> schema.</summary>
internal sealed class BookingsDatabaseInitializer(
    BookingsDbContext dbContext,
    IClock clock,
    ILogger<BookingsDatabaseInitializer> logger) : IModuleDatabaseInitializer
{
    public string ModuleName => BookingsDbContext.Schema;

    /// <summary>
    /// Last of the three. The seed data refers to vehicle and driver ids, and although nothing in
    /// the database enforces that they exist, a database seeded in this order is one where they do.
    /// </summary>
    public int Order => 30;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Bookings.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Bookings schema already seeded, leaving it alone");
            return;
        }

        var bookings = BookingsSeedData.BuildBookings(clock.UtcNow);

        dbContext.Bookings.AddRange(bookings);

        // If the generator ever produces two overlapping windows for one vehicle, the exclusion
        // constraint rejects the whole batch here rather than letting bad seed data through.
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {BookingCount} bookings", bookings.Count);
    }
}

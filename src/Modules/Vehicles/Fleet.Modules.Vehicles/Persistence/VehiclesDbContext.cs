using Fleet.Common.Persistence;
using Fleet.Modules.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Vehicles.Persistence;

/// <summary>
/// The Vehicles module's own <c>DbContext</c>, mapped entirely inside the <c>vehicles</c> schema.
/// </summary>
/// <remarks>
/// One context per module, one schema per context, and no entity from another module mapped here.
/// A query that needs a driver's name alongside a vehicle asks the Drivers module for it; it does
/// not join across schemas, because that join is exactly the coupling the boundary exists to stop.
/// </remarks>
internal sealed class VehiclesDbContext(DbContextOptions<VehiclesDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "vehicles";

    /// <summary>
    /// Where this module's migration history lives. Each module keeps its own table inside its own
    /// schema, so that migrating Vehicles never touches a row that Bookings owns.
    /// </summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Depot> Depots => Set<Depot>();

    public DbSet<OdometerReading> OdometerReadings => Set<OdometerReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VehiclesDbContext).Assembly);
    }
}

using Fleet.Common.Persistence;
using Fleet.Modules.Drivers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Drivers.Persistence;

/// <summary>The Drivers module's own <c>DbContext</c>, mapped entirely inside the <c>drivers</c> schema.</summary>
internal sealed class DriversDbContext(DbContextOptions<DriversDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "drivers";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Certificate> Certificates => Set<Certificate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DriversDbContext).Assembly);
    }
}

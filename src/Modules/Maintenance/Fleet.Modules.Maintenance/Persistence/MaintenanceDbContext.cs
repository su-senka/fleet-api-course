using Fleet.Common.Persistence;
using Fleet.Modules.Maintenance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Maintenance.Persistence;

/// <summary>The Maintenance module's own <c>DbContext</c>, mapped inside the <c>maintenance</c> schema.</summary>
internal sealed class MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "maintenance";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<PartOrderLine> PartOrderLines => Set<PartOrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MaintenanceDbContext).Assembly);
    }
}

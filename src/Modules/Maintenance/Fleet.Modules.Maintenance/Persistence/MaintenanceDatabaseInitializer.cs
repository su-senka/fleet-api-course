using Fleet.Common.Persistence;
using Fleet.Common.Time;
using Fleet.Modules.Maintenance.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Modules.Maintenance.Persistence;

/// <summary>Migrates and seeds the <c>maintenance</c> schema.</summary>
internal sealed class MaintenanceDatabaseInitializer(
    MaintenanceDbContext dbContext,
    IClock clock,
    ILogger<MaintenanceDatabaseInitializer> logger) : IModuleDatabaseInitializer
{
    public string ModuleName => MaintenanceDbContext.Schema;

    /// <summary>After Vehicles, whose ids the seed data refers to.</summary>
    public int Order => 40;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.WorkOrders.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Maintenance schema already seeded, leaving it alone");
            return;
        }

        var workOrders = MaintenanceSeedData.BuildWorkOrders(clock.UtcNow);

        dbContext.WorkOrders.AddRange(workOrders);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {WorkOrderCount} work orders with {LineCount} part lines",
            workOrders.Count,
            workOrders.Sum(workOrder => workOrder.Lines.Count));
    }
}

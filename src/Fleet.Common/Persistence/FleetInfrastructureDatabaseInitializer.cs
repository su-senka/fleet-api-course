using Microsoft.EntityFrameworkCore;

namespace Fleet.Common.Persistence;

/// <summary>Migrates the <c>shared</c> schema. Nothing to seed.</summary>
internal sealed class FleetInfrastructureDatabaseInitializer(FleetInfrastructureDbContext dbContext)
    : IModuleDatabaseInitializer
{
    public string ModuleName => FleetInfrastructureDbContext.Schema;

    /// <summary>Before every module, since it belongs to none of them.</summary>
    public int Order => 0;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

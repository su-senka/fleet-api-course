using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fleet.Modules.Maintenance.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting a host:
/// <code>
/// dotnet ef migrations add &lt;Name&gt; --project src/Modules/Maintenance/Fleet.Modules.Maintenance
/// </code>
/// </summary>
internal sealed class MaintenanceDbContextFactory : IDesignTimeDbContextFactory<MaintenanceDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet";

    public MaintenanceDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<MaintenanceDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                MaintenanceDbContext.MigrationsHistoryTable,
                MaintenanceDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new MaintenanceDbContext(options);
    }
}

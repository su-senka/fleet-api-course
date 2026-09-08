using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fleet.Common.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the infrastructure context without starting a host:
/// <code>
/// dotnet ef migrations add &lt;Name&gt; --project src/Fleet.Common
/// </code>
/// </summary>
internal sealed class FleetInfrastructureDbContextFactory : IDesignTimeDbContextFactory<FleetInfrastructureDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet";

    public FleetInfrastructureDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<FleetInfrastructureDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                FleetInfrastructureDbContext.MigrationsHistoryTable,
                FleetInfrastructureDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FleetInfrastructureDbContext(options);
    }
}

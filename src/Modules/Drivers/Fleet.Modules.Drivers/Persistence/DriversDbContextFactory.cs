using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fleet.Modules.Drivers.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting a host:
/// <code>
/// dotnet ef migrations add &lt;Name&gt; --project src/Modules/Drivers/Fleet.Modules.Drivers
/// </code>
/// </summary>
internal sealed class DriversDbContextFactory : IDesignTimeDbContextFactory<DriversDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet";

    public DriversDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<DriversDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                DriversDbContext.MigrationsHistoryTable,
                DriversDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new DriversDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fleet.Modules.Vehicles.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting a host.
/// </summary>
/// <remarks>
/// <para>
/// Without this, adding a migration to a class library means pointing EF at a startup project and
/// hoping its configuration is reachable. With it, the command is just:
/// </para>
/// <code>
/// dotnet ef migrations add &lt;Name&gt; --project src/Modules/Vehicles/Fleet.Modules.Vehicles
/// </code>
/// <para>
/// The connection string here is only ever used to work out the provider's SQL dialect at design
/// time. No migration command connects to this database.
/// </para>
/// </remarks>
internal sealed class VehiclesDbContextFactory : IDesignTimeDbContextFactory<VehiclesDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet";

    public VehiclesDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<VehiclesDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                VehiclesDbContext.MigrationsHistoryTable,
                VehiclesDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new VehiclesDbContext(options);
    }
}

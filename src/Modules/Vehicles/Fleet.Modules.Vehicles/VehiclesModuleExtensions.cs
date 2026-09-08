using Fleet.Common.Persistence;
using Fleet.Modules.Vehicles.Application;
using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Vehicles;

/// <summary>
/// The single entry point through which a host wires up the Vehicles module.
/// </summary>
/// <remarks>
/// A host calls this and learns nothing about what is inside. The module's <c>DbContext</c>,
/// entities and application services stay internal; only <c>Fleet.Modules.Vehicles.Contracts</c>
/// crosses the boundary.
/// </remarks>
public static class VehiclesModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = VehiclesDbContext.Schema;

    public static IServiceCollection AddVehiclesModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Fleet");

        services.AddDbContext<VehiclesDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Both settings are what make "schema per module" real: this context's tables and
                // its migration history all live inside the vehicles schema and nowhere else.
                npgsql.MigrationsHistoryTable(
                    VehiclesDbContext.MigrationsHistoryTable,
                    VehiclesDbContext.Schema);
            });

            // Postgres is case-sensitive and unforgiving about quoted identifiers, so the column
            // names are snake_case. Without this you end up typing "OdometerKm" in psql.
            options.UseSnakeCaseNamingConvention();
        });

        // The module's unit of work is its own DbContext. There is no shared transaction across
        // modules, which is exactly why cross-module state changes go through the outbox.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<VehiclesDbContext>());

        // The application services the API layer calls.
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IDepotService, DepotService>();

        // The narrow contracts other modules call. One implementation, registered twice, so that
        // a module can depend on just the half it needs.
        services.AddScoped<VehicleCatalog>();
        services.AddScoped<IVehicleCatalog>(provider => provider.GetRequiredService<VehicleCatalog>());
        services.AddScoped<IVehicleAvailability>(provider => provider.GetRequiredService<VehicleCatalog>());

        services.AddScoped<IModuleDatabaseInitializer, VehiclesDatabaseInitializer>();

        return services;
    }
}

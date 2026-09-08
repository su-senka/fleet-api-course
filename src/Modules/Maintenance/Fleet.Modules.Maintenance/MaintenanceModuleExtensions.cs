using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Maintenance;

/// <summary>
/// The single entry point through which a host wires up the Maintenance module.
/// </summary>
/// <remarks>
/// A host calls this and learns nothing about what is inside. The module's <c>DbContext</c>,
/// entities and application services stay internal; only <c>Fleet.Modules.Maintenance.Contracts</c>
/// crosses the boundary.
/// </remarks>
public static class MaintenanceModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = "maintenance";

    public static IServiceCollection AddMaintenanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Filled in by milestone 4: DbContext, application services, hosted services.
        return services;
    }
}

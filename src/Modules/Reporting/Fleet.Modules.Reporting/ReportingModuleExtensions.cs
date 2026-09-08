using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Reporting;

/// <summary>
/// The single entry point through which a host wires up the Reporting module.
/// </summary>
/// <remarks>
/// A host calls this and learns nothing about what is inside. The module's <c>DbContext</c>,
/// entities and application services stay internal; only <c>Fleet.Modules.Reporting.Contracts</c>
/// crosses the boundary.
/// </remarks>
public static class ReportingModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = "reporting";

    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Filled in by milestone 5: DbContext, application services, hosted services.
        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Notifications;

/// <summary>
/// The single entry point through which a host wires up the Notifications module.
/// </summary>
/// <remarks>
/// A host calls this and learns nothing about what is inside. The module's <c>DbContext</c>,
/// entities and application services stay internal; only <c>Fleet.Modules.Notifications.Contracts</c>
/// crosses the boundary.
/// </remarks>
public static class NotificationsModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = "notifications";

    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Filled in by milestone 5: DbContext, application services, hosted services.
        return services;
    }
}

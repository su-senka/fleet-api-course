using Fleet.Common.Messaging;
using Fleet.Common.Persistence;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Notifications.Application;
using Fleet.Modules.Notifications.Contracts;
using Fleet.Modules.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Notifications;

/// <summary>
/// The single entry point through which a host wires up the Notifications module.
/// </summary>
public static class NotificationsModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = NotificationsDbContext.Schema;

    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Fleet");

        services.AddDbContext<NotificationsDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                NotificationsDbContext.MigrationsHistoryTable,
                NotificationsDbContext.Schema));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<NotificationsDbContext>());

        services.AddScoped<INotificationService, NotificationService>();

        // The subscription. This one line is the entire coupling between Drivers and Notifications:
        // the event bus finds this handler by its closed generic interface, and Drivers never knows
        // it was registered.
        services.AddScoped<IIntegrationEventHandler<CertificateExpiringSoon>, CertificateExpiringSoonHandler>();

        services.AddScoped<IModuleDatabaseInitializer, NotificationsDatabaseInitializer>();

        return services;
    }
}

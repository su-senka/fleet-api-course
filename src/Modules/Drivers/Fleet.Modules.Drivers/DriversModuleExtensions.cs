using Fleet.Common;
using Fleet.Common.Persistence;
using Fleet.Modules.Drivers.Application;
using Fleet.Modules.Drivers.Hosting;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Drivers;

/// <summary>
/// The single entry point through which a host wires up the Drivers module.
/// </summary>
/// <remarks>
/// Identical in shape to <c>AddVehiclesModule</c>, on purpose. Six modules that each register
/// themselves a slightly different way is six things to learn instead of one.
/// </remarks>
public static class DriversModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = DriversDbContext.Schema;

    public static IServiceCollection AddDriversModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Fleet");

        services.AddDbContext<DriversDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                DriversDbContext.MigrationsHistoryTable,
                DriversDbContext.Schema));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DriversDbContext>());

        services.AddScoped<IDriverService, DriverService>();

        services.AddScoped<DriverDirectory>();
        services.AddScoped<IDriverDirectory>(provider => provider.GetRequiredService<DriverDirectory>());
        services.AddScoped<IDriverEligibility>(provider => provider.GetRequiredService<DriverDirectory>());

        services.AddScoped<IModuleDatabaseInitializer, DriversDatabaseInitializer>();

        // This module publishes CertificateExpiringSoon, so it needs an outbox of its own. The
        // table is mapped into the drivers schema by DriversDbContext.
        services.AddModuleOutbox<DriversDbContext>();

        services.Configure<CertificateExpiryOptions>(
            configuration.GetSection(CertificateExpiryOptions.SectionName));

        services.AddHostedService<CertificateExpiryScanner>();

        return services;
    }
}

using Fleet.Common.Persistence;
using Fleet.Modules.Reporting.Application;
using Fleet.Modules.Reporting.Contracts;
using Fleet.Modules.Reporting.Hosting;
using Fleet.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Reporting;

/// <summary>
/// The single entry point through which a host wires up the Reporting module.
/// </summary>
/// <remarks>
/// This module reads both Vehicles and Bookings through their contracts, and writes to the blob
/// store, so a host must register those first. <c>AddFleetInfrastructure</c> supplies the blob
/// store.
/// </remarks>
public static class ReportingModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = ReportingDbContext.Schema;

    public static IServiceCollection AddReportingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Fleet");

        services.AddDbContext<ReportingDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                ReportingDbContext.MigrationsHistoryTable,
                ReportingDbContext.Schema));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ReportingDbContext>());

        services.AddScoped<IReportService, ReportService>();

        services.Configure<ReportingOptions>(configuration.GetSection(ReportingOptions.SectionName));
        services.AddHostedService<ReportJobProcessor>();

        services.AddScoped<IModuleDatabaseInitializer, ReportingDatabaseInitializer>();

        return services;
    }
}

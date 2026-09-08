using Fleet.Common.Persistence;
using Fleet.Modules.Maintenance.Application;
using Fleet.Modules.Maintenance.Contracts;
using Fleet.Modules.Maintenance.Persistence;
using Fleet.Modules.Maintenance.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Maintenance;

/// <summary>
/// The single entry point through which a host wires up the Maintenance module.
/// </summary>
public static class MaintenanceModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = MaintenanceDbContext.Schema;

    public static IServiceCollection AddMaintenanceModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Fleet");

        services.AddDbContext<MaintenanceDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                MaintenanceDbContext.MigrationsHistoryTable,
                MaintenanceDbContext.Schema));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<MaintenanceDbContext>());

        var supplierBaseUrl = configuration["Supplier:BaseUrl"] ?? "http://localhost:5080/";

        // -----------------------------------------------------------------------------------
        // TODO(week-12): this typed client has no resilience at all. Give it some.
        //
        // As registered, it will:
        //   - wait up to HttpClient's default of 100 seconds for a supplier that stalls for 8;
        //   - give up on the first 500, of which the supplier returns one in four;
        //   - ignore the Retry-After header on a 429 and come straight back;
        //   - keep hammering an upstream that is in a 60-second outage.
        //
        // Your job is to add a per-attempt timeout, a retry with backoff and jitter that honours
        // Retry-After, and a circuit breaker that stops calling a supplier that is plainly down.
        // Microsoft.Extensions.Http.Resilience and Polly.Core are already in
        // Directory.Packages.props, so `AddStandardResilienceHandler()` is one line away - but
        // read what its defaults actually are before you accept them.
        //
        // Two questions worth answering before you write any of it:
        //   - retrying a POST that creates an order is only safe if the supplier is idempotent.
        //     Is it? What would you need from it to make retrying safe?
        //   - when the breaker is open, what should OrderPartsAsync return, and what status code
        //     should the endpoint turn that into?
        //
        // See docs/assignments/week-12.md.
        // -----------------------------------------------------------------------------------
        services.AddHttpClient<ISupplierClient, SupplierClient>(client =>
        {
            client.BaseAddress = new Uri(supplierBaseUrl);
        });

        services.AddScoped<IMaintenanceService, MaintenanceService>();

        services.AddScoped<IModuleDatabaseInitializer, MaintenanceDatabaseInitializer>();

        return services;
    }
}

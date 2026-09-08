using Azure.Storage.Blobs;
using Fleet.Common.Idempotency;
using Fleet.Common.Messaging;
using Fleet.Common.Messaging.Outbox;
using Fleet.Common.Persistence;
using Fleet.Common.Storage;
using Fleet.Common.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Common;

/// <summary>Registers the shared-kernel services that every host needs.</summary>
public static class FleetCommonExtensions
{
    /// <summary>
    /// Adds the handful of cross-cutting services that are not owned by any single module.
    /// Call this once, before the <c>Add*Module</c> calls.
    /// </summary>
    public static IServiceCollection AddFleetCommon(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddScoped<IEventBus, InProcessEventBus>();

        return services;
    }

    /// <summary>
    /// Adds the infrastructure the shared kernel owns: blob storage, the idempotency store, and
    /// the background service that drains every module's outbox.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="AddFleetCommon"/> because it needs configuration and touches real
    /// external services, which a unit test wiring up one module has no use for.
    /// </remarks>
    public static IServiceCollection AddFleetInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Fleet");

        services.AddDbContext<FleetInfrastructureDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                FleetInfrastructureDbContext.MigrationsHistoryTable,
                FleetInfrastructureDbContext.Schema));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IIdempotencyStore, PostgresIdempotencyStore>();
        services.AddScoped<IModuleDatabaseInitializer, FleetInfrastructureDatabaseInitializer>();

        var blobConnectionString = configuration.GetConnectionString("Blobs");

        if (!string.IsNullOrWhiteSpace(blobConnectionString))
        {
            // One BlobServiceClient for the process. It is thread-safe, pools connections, and
            // creating one per request is the standard way to exhaust sockets.
            services.AddSingleton(new BlobServiceClient(blobConnectionString));
            services.AddScoped<IBlobStore, AzureBlobStore>();
        }

        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    /// <summary>
    /// Registers an outbox and its dispatcher over a module's own <c>DbContext</c>.
    /// </summary>
    /// <remarks>
    /// Called from the module's <c>Add*Module</c> method. The module must also map
    /// <see cref="OutboxMessage"/> in its <c>DbContext</c>, with
    /// <see cref="OutboxMessageConfiguration"/>, so the table lands in its own schema.
    /// </remarks>
    public static IServiceCollection AddModuleOutbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IOutbox, EfOutbox<TDbContext>>();
        services.AddScoped<IOutboxDispatcher, EfOutboxDispatcher<TDbContext>>();

        return services;
    }
}

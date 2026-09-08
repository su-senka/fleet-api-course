using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fleet.Common.Persistence;

/// <summary>Runs every registered <see cref="IModuleDatabaseInitializer"/>, in order.</summary>
public static class ModuleDatabaseInitialization
{
    /// <summary>
    /// Migrates every module's schema and optionally seeds it.
    /// </summary>
    /// <remarks>
    /// Migrating on startup is a development convenience, not a deployment strategy: two
    /// instances starting at once would race, and a bad migration takes the application down with
    /// it. Real deployments run migrations as a separate step before the new version starts. The
    /// hosts here only call this in Development, or when asked to by <c>--seed</c>.
    /// </remarks>
    public static async Task InitializeModulesAsync(
        this IHost host,
        bool seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        await using var scope = host.Services.CreateAsyncScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ModuleDatabaseInitialization));

        var initializers = scope.ServiceProvider
            .GetServices<IModuleDatabaseInitializer>()
            .OrderBy(initializer => initializer.Order)
            .ToList();

        foreach (var initializer in initializers)
        {
            logger.LogInformation("Migrating schema for module {Module}", initializer.ModuleName);
            await initializer.MigrateAsync(cancellationToken);
        }

        if (!seed)
        {
            return;
        }

        // Seeding is a second pass rather than part of the loop above, because a module's seed
        // data may assume that every other module's schema already exists.
        foreach (var initializer in initializers)
        {
            logger.LogInformation("Seeding module {Module}", initializer.ModuleName);
            await initializer.SeedAsync(cancellationToken);
        }
    }
}

using Fleet.Common.Persistence;
using Fleet.Modules.Bookings.Application;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Bookings;

/// <summary>
/// The single entry point through which a host wires up the Bookings module.
/// </summary>
/// <remarks>
/// This module needs Vehicles and Drivers to be registered too, because its service depends on
/// their contracts. The host calls all three; nothing here reaches out and registers them, which
/// would hide the dependency and make the order of the calls in <c>Program.cs</c> a mystery.
/// </remarks>
public static class BookingsModuleExtensions
{
    /// <summary>The Postgres schema this module owns. No other module writes to it.</summary>
    public const string SchemaName = BookingsDbContext.Schema;

    public static IServiceCollection AddBookingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Fleet");

        services.AddDbContext<BookingsDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                BookingsDbContext.MigrationsHistoryTable,
                BookingsDbContext.Schema));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<BookingsDbContext>());

        services.AddScoped<IBookingService, BookingService>();

        // The narrow read contract other modules use. Reporting needs it; nothing else does.
        services.AddScoped<IBookingCalendar, BookingCalendar>();

        services.AddScoped<IModuleDatabaseInitializer, BookingsDatabaseInitializer>();

        return services;
    }
}

using Fleet.Common;
using Fleet.Common.Persistence;
using Fleet.Common.Results;
using Fleet.Common.Seeding;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Domain;
using Fleet.Modules.Bookings.Persistence;
using Fleet.Modules.Drivers;
using Fleet.Modules.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Fleet.Modules.Bookings.Tests;

/// <summary>
/// The rule that one vehicle cannot be booked twice, tested against a real Postgres.
/// </summary>
/// <remarks>
/// <para>
/// The service checks for a clash before it inserts, and that check is a lie under concurrency:
/// between the SELECT and the INSERT, another transaction can commit. These tests exist to show
/// that it does not matter, because the exclusion constraint is underneath.
/// </para>
/// <para>
/// This is the only place in the module tests that needs a database. Everything else about
/// bookings - the overlap arithmetic, the state machine, the ETag token - is tested without one.
/// </para>
/// </remarks>
public sealed class DoubleBookingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    /// <summary>Vehicle 1 in the seed data is Available; vehicle 0 is retired and 3 is in the workshop.</summary>
    private static readonly Guid BookableVehicleId = DeterministicGuid.Create("vehicle", 1);

    /// <summary>Driver 0 holds a licence valid for years. Drivers 7, 17 and 22 deliberately do not.</summary>
    private static readonly Guid EligibleDriverId = DeterministicGuid.Create("driver", 0);

    private static readonly DateTimeOffset Start = new(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(4);

    [Fact]
    public async Task Two_parallel_bookings_for_the_same_window_yield_one_success_and_one_conflict()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        // Two independent scopes, so each request gets its own DbContext and its own transaction -
        // exactly as two concurrent HTTP requests would.
        var command = new CreateBookingCommand(
            BookableVehicleId, EligibleDriverId, "Rozvoz Praha - Brno", Start, End);

        // A barrier so both calls reach the database at genuinely the same moment. Without it one
        // request finishes before the other starts and the race never happens.
        using var gate = new Barrier(2);

        async Task<Result<BookingDto>> BookAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

            gate.SignalAndWait();
            return await service.BookAsync(command, TestContext.Current.CancellationToken);
        }

        var results = await Task.WhenAll(Task.Run(BookAsync), Task.Run(BookAsync));

        var succeeded = results.Where(result => result.IsSuccess).ToList();
        var failed = results.Where(result => result.IsFailure).ToList();

        Assert.Single(succeeded);
        Assert.Single(failed);

        // The loser must get a Conflict - which an endpoint maps to 409 - and never an unhandled
        // exception. Whether the service's own check caught it or the constraint did is an
        // implementation detail the caller cannot see, and that is the point.
        Assert.Equal(ErrorKind.Conflict, failed[0].Error!.Kind);
        Assert.Equal("booking.overlaps_existing", failed[0].Error!.Code);

        await AssertBookingCountAsync(provider, expected: 1);
    }

    [Fact]
    public async Task The_database_refuses_an_overlap_even_when_the_service_check_is_bypassed()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await InsertDirectlyAsync(provider, Start, End);

        // Straight to the DbContext, skipping BookingService entirely. This is what a second
        // transaction committing at the wrong moment looks like from the database's point of view.
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => InsertDirectlyAsync(provider, Start.AddHours(1), End.AddHours(1)));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgresException.SqlState);
        Assert.Equal(BookingsDbContext.NoOverlapConstraintName, postgresException.ConstraintName);
    }

    [Fact]
    public async Task The_database_allows_bookings_that_touch_but_do_not_overlap()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await InsertDirectlyAsync(provider, Start, End);

        // Starts at the exact instant the first one ends. The half-open range in the constraint is
        // what makes this legal, and it is the case the seed data contains on purpose.
        await InsertDirectlyAsync(provider, End, End.AddHours(3));

        await AssertBookingCountAsync(provider, expected: 2);
    }

    [Fact]
    public async Task The_database_allows_a_cancelled_booking_to_overlap_a_confirmed_one()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await InsertDirectlyAsync(provider, Start, End);

        // The constraint is partial - WHERE (status <> 2) - so a cancelled booking holds nothing.
        // Without that, cancelling would not really free the slot.
        await InsertDirectlyAsync(provider, Start, End, cancelled: true);

        await AssertBookingCountAsync(provider, expected: 2);
    }

    [Fact]
    public async Task Rescheduling_onto_an_occupied_window_is_a_conflict()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var morning = await service.BookAsync(
            new CreateBookingCommand(BookableVehicleId, EligibleDriverId, "Rano", Start, End),
            cancellationToken);

        var evening = await service.BookAsync(
            new CreateBookingCommand(
                BookableVehicleId, EligibleDriverId, "Vecer", Start.AddHours(8), End.AddHours(8)),
            cancellationToken);

        Assert.True(morning.IsSuccess);
        Assert.True(evening.IsSuccess);

        var moved = await service.RescheduleAsync(
            evening.Value.Id,
            new RescheduleBookingCommand(Start, End),
            cancellationToken);

        Assert.True(moved.IsFailure);
        Assert.Equal("booking.overlaps_existing", moved.Error!.Code);
    }

    [Fact]
    public async Task A_booking_can_be_rescheduled_onto_a_window_it_already_occupies()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var booked = await service.BookAsync(
            new CreateBookingCommand(BookableVehicleId, EligibleDriverId, "Sluzebni cesta", Start, End),
            cancellationToken);

        // Extending a booking by an hour must not conflict with itself, which is why the overlap
        // query excludes the row being moved.
        var extended = await service.RescheduleAsync(
            booked.Value.Id,
            new RescheduleBookingCommand(Start, End.AddHours(1)),
            cancellationToken);

        Assert.True(extended.IsSuccess);
        Assert.Equal(End.AddHours(1), extended.Value.EndsAt);
    }

    [Fact]
    public async Task The_row_version_changes_on_every_write_and_a_stale_one_is_refused()
    {
        postgres.SkipIfUnavailable();
        await using var provider = await BuildProviderAsync();

        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var booked = await service.BookAsync(
            new CreateBookingCommand(BookableVehicleId, EligibleDriverId, "Sluzebni cesta", Start, End),
            cancellationToken);

        var original = booked.Value.RowVersion;

        var moved = await service.RescheduleAsync(
            booked.Value.Id,
            new RescheduleBookingCommand(Start.AddDays(1), End.AddDays(1), original),
            cancellationToken);

        Assert.True(moved.IsSuccess);

        // xmin is the transaction id that last wrote the row, so it necessarily changed.
        Assert.NotEqual(original, moved.Value.RowVersion);

        // Replaying the first version is what a client with a stale ETag does. It must be refused
        // rather than silently overwriting whoever moved the booking in between.
        var stale = await service.RescheduleAsync(
            booked.Value.Id,
            new RescheduleBookingCommand(Start.AddDays(2), End.AddDays(2), original),
            cancellationToken);

        Assert.True(stale.IsFailure);
        Assert.Equal(ErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal("booking.version_mismatch", stale.Error!.Code);
    }

    // ---------------------------------------------------------------------------------------
    // Harness
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Wires up all three modules against the throwaway database, migrates every schema, and seeds
    /// vehicles and drivers - but not bookings, so each test starts with an empty calendar.
    /// </summary>
    private async Task<ServiceProvider> BuildProviderAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Fleet"] = postgres.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddFleetCommon();
        services.AddVehiclesModule(configuration);
        services.AddDriversModule(configuration);
        services.AddBookingsModule(configuration);

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var initializers = scope.ServiceProvider
                .GetServices<IModuleDatabaseInitializer>()
                .OrderBy(initializer => initializer.Order)
                .ToList();

            foreach (var initializer in initializers)
            {
                await initializer.MigrateAsync(TestContext.Current.CancellationToken);
            }

            foreach (var initializer in initializers.Where(i => i.ModuleName != BookingsDbContext.Schema))
            {
                await initializer.SeedAsync(TestContext.Current.CancellationToken);
            }

            // The container is shared across the class, so wipe the calendar between tests.
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "TRUNCATE bookings.bookings;", TestContext.Current.CancellationToken);
        }

        return provider;
    }

    /// <summary>Writes a booking straight to the table, without going near the service's checks.</summary>
    private static async Task InsertDirectlyAsync(
        ServiceProvider provider,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool cancelled = false)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var booking = Booking.Create(
            Guid.CreateVersion7(),
            BookableVehicleId,
            EligibleDriverId,
            "Primy zapis",
            startsAt,
            endsAt,
            DateTimeOffset.UtcNow).Value;

        if (cancelled)
        {
            booking.Cancel(DateTimeOffset.UtcNow);
        }

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task AssertBookingCountAsync(ServiceProvider provider, int expected)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var actual = await dbContext.Bookings.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }
}

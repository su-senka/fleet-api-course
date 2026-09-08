using Fleet.Common.Seeding;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Domain;
using Fleet.Modules.Bookings.Seeding;

namespace Fleet.Modules.Bookings.Tests;

/// <summary>
/// The booking seed data.
/// </summary>
/// <remarks>
/// The most important assertion here is <see cref="No_two_bookings_for_one_vehicle_overlap"/>. If
/// the generator ever produces a clash, the exclusion constraint rejects the whole batch and
/// <c>make reset</c> fails with a Postgres error rather than a useful message. Catching it here
/// costs nothing and explains itself.
/// </remarks>
public sealed class BookingsSeedDataTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void It_produces_about_three_thousand_bookings()
    {
        var bookings = BookingsSeedData.BuildBookings(Anchor);

        // 250 vehicles times 12 slots, plus the two deliberately awkward extras.
        Assert.Equal(3_002, bookings.Count);
    }

    [Fact]
    public void No_two_bookings_for_one_vehicle_overlap()
    {
        var bookings = BookingsSeedData.BuildBookings(Anchor);

        foreach (var perVehicle in bookings.GroupBy(booking => booking.VehicleId))
        {
            // Cancelled bookings are excluded for the same reason the constraint excludes them:
            // they hold nothing, and one of them overlaps a confirmed booking on purpose.
            var blocking = perVehicle
                .Where(booking => booking.BlocksVehicle)
                .OrderBy(booking => booking.StartsAt)
                .ToList();

            for (var i = 1; i < blocking.Count; i++)
            {
                Assert.False(
                    blocking[i - 1].Window.Overlaps(blocking[i].Window),
                    $"Vehicle {perVehicle.Key} has overlapping bookings "
                    + $"{blocking[i - 1].Window} and {blocking[i].Window}. "
                    + "The exclusion constraint will reject the seed.");
            }
        }
    }

    [Fact]
    public void It_produces_the_same_rows_every_time()
    {
        var first = BookingsSeedData.BuildBookings(Anchor);
        var second = BookingsSeedData.BuildBookings(Anchor);

        Assert.Equal(
            first.Select(booking => (booking.Id, booking.VehicleId, booking.StartsAt)),
            second.Select(booking => (booking.Id, booking.VehicleId, booking.StartsAt)));
    }

    [Fact]
    public void It_refers_to_vehicles_and_drivers_the_other_seeders_created()
    {
        // Bookings never reads the vehicles or drivers tables. Both sides derive the same ids from
        // the same names, which is what lets the modules agree without talking to each other.
        var bookings = BookingsSeedData.BuildBookings(Anchor);

        var expectedVehicleIds = Enumerable
            .Range(0, BookingsSeedData.VehicleCount)
            .Select(index => DeterministicGuid.Create("vehicle", index))
            .ToHashSet();

        var expectedDriverIds = Enumerable
            .Range(0, BookingsSeedData.DriverCount)
            .Select(index => DeterministicGuid.Create("driver", index))
            .ToHashSet();

        Assert.All(bookings, booking => Assert.Contains(booking.VehicleId, expectedVehicleIds));
        Assert.All(bookings, booking => Assert.Contains(booking.DriverId, expectedDriverIds));
    }

    [Fact]
    public void One_vehicle_has_two_bookings_that_touch_without_overlapping()
    {
        // The edge case students get wrong. It is in the data on purpose so that a subtly wrong
        // overlap check fails against the seed rather than in production.
        var bookings = BookingsSeedData.BuildBookings(Anchor);

        var vehicleId = DeterministicGuid.Create("vehicle", BookingsSeedData.AdjacentPairVehicleIndex);

        var touching = bookings
            .Where(booking => booking.VehicleId == vehicleId)
            .OrderBy(booking => booking.StartsAt)
            .Zip(
                bookings
                    .Where(booking => booking.VehicleId == vehicleId)
                    .OrderBy(booking => booking.StartsAt)
                    .Skip(1))
            .Count(pair => pair.First.EndsAt == pair.Second.StartsAt);

        Assert.Equal(1, touching);
    }

    [Fact]
    public void One_vehicle_has_a_cancelled_booking_overlapping_a_live_one()
    {
        var bookings = BookingsSeedData.BuildBookings(Anchor);

        var vehicleId = DeterministicGuid.Create("vehicle", BookingsSeedData.OverlappingCancelledVehicleIndex);
        var forVehicle = bookings.Where(booking => booking.VehicleId == vehicleId).ToList();

        var cancelled = forVehicle.Where(booking => !booking.BlocksVehicle).ToList();
        var live = forVehicle.Where(booking => booking.BlocksVehicle).ToList();

        Assert.Contains(
            cancelled,
            gone => live.Any(alive => alive.Window.Overlaps(gone.Window)));
    }

    [Fact]
    public void Every_status_is_represented()
    {
        var bookings = BookingsSeedData.BuildBookings(Anchor);
        var byStatus = bookings.GroupBy(booking => booking.Status).ToDictionary(g => g.Key, g => g.Count());

        // Confirmed bookings are the ones still to come. Without any, the calendar is empty and
        // there is nothing for a "what is booked this week" endpoint to return.
        Assert.True(byStatus[BookingStatus.Confirmed] > 100, "There should be future bookings.");
        Assert.True(byStatus[BookingStatus.Completed] > 1_000);
        Assert.True(byStatus[BookingStatus.Cancelled] > 100);
    }

    [Fact]
    public void Bookings_span_roughly_eighteen_months_around_the_anchor()
    {
        var bookings = BookingsSeedData.BuildBookings(Anchor);

        var earliest = bookings.Min(booking => booking.StartsAt);
        var latest = bookings.Max(booking => booking.EndsAt);

        Assert.True(earliest >= Anchor.AddMonths(-17), $"Earliest booking {earliest:u} is too far back.");
        Assert.True(earliest <= Anchor.AddMonths(-15), $"Earliest booking {earliest:u} is not far enough back.");
        Assert.True(latest > Anchor, "Some bookings must be in the future.");
        Assert.True((latest - earliest).TotalDays > 500, "The span should cover about 18 months.");
    }

    [Fact]
    public void Every_booking_has_a_sane_window()
    {
        var bookings = BookingsSeedData.BuildBookings(Anchor);

        Assert.All(bookings, booking =>
        {
            Assert.True(booking.EndsAt > booking.StartsAt);
            Assert.True(booking.Window.Duration >= Booking.MinimumDuration);
            Assert.True(booking.Window.Duration <= Booking.MaximumDuration);
        });
    }
}

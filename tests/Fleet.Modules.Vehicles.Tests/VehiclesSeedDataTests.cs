using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Seeding;

namespace Fleet.Modules.Vehicles.Tests;

/// <summary>
/// The seed data is part of the teaching material, so its shape is asserted like anything else.
/// </summary>
/// <remarks>
/// A student who opens the vehicle list and sees 250 rows, some of them in maintenance, is looking
/// at something these tests guarantee. If the generator drifts, this fails before anyone notices
/// by hand.
/// </remarks>
public sealed class VehiclesSeedDataTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void It_produces_the_volumes_the_course_needs()
    {
        var depots = VehiclesSeedData.BuildDepots();
        var vehicles = VehiclesSeedData.BuildVehicles(depots, Anchor);

        Assert.Equal(4, depots.Count);
        Assert.Equal(250, vehicles.Count);
        Assert.Equal(15_000, vehicles.Sum(vehicle => vehicle.OdometerReadings.Count));
    }

    [Fact]
    public void It_produces_the_same_rows_every_time()
    {
        // The whole point of DeterministicGuid and a fixed RNG seed. If this fails, a `make reset`
        // has stopped being repeatable and every hard-coded id in the .http files is now a 404.
        var first = VehiclesSeedData.BuildVehicles(VehiclesSeedData.BuildDepots(), Anchor);
        var second = VehiclesSeedData.BuildVehicles(VehiclesSeedData.BuildDepots(), Anchor);

        Assert.Equal(
            first.Select(vehicle => (vehicle.Id, vehicle.Plate, vehicle.OdometerKm)),
            second.Select(vehicle => (vehicle.Id, vehicle.Plate, vehicle.OdometerKm)));
    }

    [Fact]
    public void Every_plate_is_unique()
    {
        var vehicles = VehiclesSeedData.BuildVehicles(VehiclesSeedData.BuildDepots(), Anchor);

        Assert.Equal(vehicles.Count, vehicles.Select(vehicle => vehicle.Plate).Distinct().Count());
    }

    [Fact]
    public void Every_vehicle_has_a_monotonic_reading_history_ending_at_its_current_mileage()
    {
        var vehicles = VehiclesSeedData.BuildVehicles(VehiclesSeedData.BuildDepots(), Anchor);

        foreach (var vehicle in vehicles)
        {
            var readings = vehicle.OdometerReadings.ToList();

            for (var i = 1; i < readings.Count; i++)
            {
                Assert.True(
                    readings[i].Km >= readings[i - 1].Km,
                    $"Vehicle {vehicle.Plate} has a reading that goes backwards at index {i}.");
                Assert.True(
                    readings[i].RecordedAt >= readings[i - 1].RecordedAt,
                    $"Vehicle {vehicle.Plate} has a reading out of chronological order at index {i}.");
            }

            Assert.Equal(readings[^1].Km, vehicle.OdometerKm);
        }
    }

    [Fact]
    public void There_are_vehicles_in_every_status_so_the_filters_have_something_to_find()
    {
        var vehicles = VehiclesSeedData.BuildVehicles(VehiclesSeedData.BuildDepots(), Anchor);

        var byStatus = vehicles.GroupBy(vehicle => vehicle.Status).ToDictionary(g => g.Key, g => g.Count());

        Assert.True(byStatus[VehicleStatus.Available] > 200);
        Assert.True(byStatus[VehicleStatus.InMaintenance] >= 10, "Something has to be unbookable.");
        Assert.True(byStatus[VehicleStatus.Retired] >= 10);
    }

    [Fact]
    public void Vehicles_are_spread_across_all_four_depots()
    {
        var depots = VehiclesSeedData.BuildDepots();
        var vehicles = VehiclesSeedData.BuildVehicles(depots, Anchor);

        foreach (var depot in depots)
        {
            Assert.True(
                vehicles.Count(vehicle => vehicle.DepotId == depot.Id) > 50,
                $"Depot {depot.Name} has too few vehicles to page through.");
        }
    }

    [Fact]
    public void Readings_span_roughly_eighteen_months_before_the_anchor()
    {
        var vehicles = VehiclesSeedData.BuildVehicles(VehiclesSeedData.BuildDepots(), Anchor);
        var readings = vehicles[0].OdometerReadings.ToList();

        Assert.True(readings[0].RecordedAt >= Anchor.AddMonths(-19));
        Assert.True(readings[^1].RecordedAt <= Anchor);
    }
}

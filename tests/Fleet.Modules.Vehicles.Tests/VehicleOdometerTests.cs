using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Domain;

namespace Fleet.Modules.Vehicles.Tests;

/// <summary>
/// The odometer rule: mileage only ever goes up.
/// </summary>
/// <remarks>
/// These run against the entity directly, with no database anywhere. That is the payoff for
/// putting the rule on <see cref="Vehicle"/> rather than in the service.
/// </remarks>
public sealed class VehicleOdometerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static Vehicle NewVehicle(int odometerKm = 10_000) =>
        Vehicle.Register(Guid.CreateVersion7(), "1AB 2345", VehicleType.Van, Guid.CreateVersion7(), odometerKm).Value;

    [Fact]
    public void A_higher_reading_is_accepted_and_moves_the_odometer()
    {
        var vehicle = NewVehicle(odometerKm: 10_000);

        var result = vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, 10_450);

        Assert.True(result.IsSuccess);
        Assert.Equal(10_450, vehicle.OdometerKm);
        Assert.Equal(Noon, vehicle.LastReadingAt);
        Assert.Single(vehicle.OdometerReadings);
    }

    [Fact]
    public void A_lower_reading_is_rejected_as_a_validation_failure()
    {
        var vehicle = NewVehicle(odometerKm: 10_000);

        var result = vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, 9_999);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal("odometer.not_monotonic", result.Error.Code);
    }

    [Fact]
    public void A_rejected_reading_leaves_the_vehicle_untouched()
    {
        var vehicle = NewVehicle(odometerKm: 10_000);

        vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, 9_000);

        Assert.Equal(10_000, vehicle.OdometerKm);
        Assert.Empty(vehicle.OdometerReadings);
        Assert.Null(vehicle.LastReadingAt);
    }

    [Fact]
    public void An_identical_reading_is_accepted_because_a_parked_vehicle_travels_nothing()
    {
        var vehicle = NewVehicle(odometerKm: 10_000);

        var result = vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, 10_000);

        Assert.True(result.IsSuccess);
        Assert.Equal(10_000, vehicle.OdometerKm);
    }

    [Fact]
    public void A_reading_dated_before_the_previous_one_is_rejected()
    {
        var vehicle = NewVehicle(odometerKm: 10_000);
        vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, 10_400);

        var result = vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon.AddHours(-1), 10_500);

        Assert.True(result.IsFailure);
        Assert.Equal("odometer.out_of_order", result.Error.Code);
    }

    [Fact]
    public void A_reading_at_exactly_the_previous_timestamp_is_accepted()
    {
        // Two readings in the same second is odd but not wrong, and rejecting it would make a
        // bulk import of a day's readings fail for no good reason.
        var vehicle = NewVehicle(odometerKm: 10_000);
        vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, 10_400);

        var result = vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, 10_500);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, vehicle.OdometerReadings.Count);
    }

    [Fact]
    public void A_negative_reading_is_rejected()
    {
        var vehicle = NewVehicle(odometerKm: 10_000);

        var result = vehicle.RecordOdometerReading(Guid.CreateVersion7(), Noon, -1);

        Assert.True(result.IsFailure);
        Assert.Equal("odometer.negative", result.Error.Code);
    }
}

using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Domain;

namespace Fleet.Modules.Vehicles.Tests;

/// <summary>The rule that a vehicle in the workshop cannot be booked.</summary>
public sealed class VehicleStatusTests
{
    private static Vehicle NewVehicle() =>
        Vehicle.Register(Guid.CreateVersion7(), "2CD 3456", VehicleType.Truck, Guid.CreateVersion7(), 1_000).Value;

    [Fact]
    public void A_new_vehicle_is_available_and_bookable()
    {
        var vehicle = NewVehicle();

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.True(vehicle.IsBookable);
    }

    [Theory]
    [InlineData(VehicleStatus.InMaintenance)]
    [InlineData(VehicleStatus.Retired)]
    public void Only_an_available_vehicle_is_bookable(VehicleStatus status)
    {
        var vehicle = NewVehicle();

        vehicle.ChangeStatus(status);

        Assert.False(vehicle.IsBookable);
    }

    [Fact]
    public void A_vehicle_can_come_back_from_the_workshop()
    {
        var vehicle = NewVehicle();
        vehicle.ChangeStatus(VehicleStatus.InMaintenance);

        var result = vehicle.ChangeStatus(VehicleStatus.Available);

        Assert.True(result.IsSuccess);
        Assert.True(vehicle.IsBookable);
    }

    [Fact]
    public void A_retired_vehicle_cannot_return_to_service()
    {
        var vehicle = NewVehicle();
        vehicle.ChangeStatus(VehicleStatus.Retired);

        var result = vehicle.ChangeStatus(VehicleStatus.Available);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal("vehicle.retired", result.Error.Code);
    }

    [Fact]
    public void Plates_are_normalised_to_upper_case()
    {
        var vehicle = Vehicle.Register(
            Guid.CreateVersion7(), "  3ef 7890 ", VehicleType.Car, Guid.CreateVersion7(), 0).Value;

        Assert.Equal("3EF 7890", vehicle.Plate);
    }

    [Fact]
    public void A_vehicle_without_a_plate_is_rejected()
    {
        var result = Vehicle.Register(Guid.CreateVersion7(), "   ", VehicleType.Car, Guid.CreateVersion7(), 0);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal("vehicle.plate_required", result.Error.Code);
    }
}

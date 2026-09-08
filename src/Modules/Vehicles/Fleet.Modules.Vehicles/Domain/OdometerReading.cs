namespace Fleet.Modules.Vehicles.Domain;

/// <summary>
/// One mileage reading, taken at a point in time.
/// </summary>
/// <remarks>
/// Readings are append-only history. The vehicle's current mileage is a separate column that this
/// history is expected to agree with - see <see cref="Vehicle.RecordOdometerReading"/>, which is
/// the only thing that writes either.
/// </remarks>
internal sealed class OdometerReading
{
    private OdometerReading()
    {
    }

    internal OdometerReading(Guid id, Guid vehicleId, DateTimeOffset recordedAt, int km)
    {
        Id = id;
        VehicleId = vehicleId;
        RecordedAt = recordedAt;
        Km = km;
    }

    public Guid Id { get; private set; }

    public Guid VehicleId { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public int Km { get; private set; }
}

using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Modules.Vehicles.Domain;

/// <summary>
/// A vehicle in the fleet.
/// </summary>
/// <remarks>
/// The two rules from the brief live here rather than in the service: mileage only ever goes up,
/// and a vehicle that is not <see cref="VehicleStatus.Available"/> cannot be booked. Putting them
/// on the entity means there is exactly one place they can be broken, and the unit tests can
/// reach them without a database.
/// </remarks>
internal sealed class Vehicle
{
    private readonly List<OdometerReading> _odometerReadings = [];

    private Vehicle() => Plate = string.Empty;

    private Vehicle(Guid id, string plate, VehicleType type, Guid depotId, int odometerKm)
    {
        Id = id;
        Plate = plate;
        Type = type;
        DepotId = depotId;
        OdometerKm = odometerKm;
        Status = VehicleStatus.Available;
    }

    public Guid Id { get; private set; }

    /// <summary>The registration plate. Unique across the fleet, and normalised to upper case.</summary>
    public string Plate { get; private set; }

    public VehicleType Type { get; private set; }

    public VehicleStatus Status { get; private set; }

    /// <summary>
    /// Current mileage. Denormalised from <see cref="OdometerReadings"/> on purpose: every list
    /// of vehicles wants it, and recomputing a MAX over 15,000 readings to render one page would
    /// be a poor trade.
    /// </summary>
    public int OdometerKm { get; private set; }

    public Guid DepotId { get; private set; }

    public Depot? Depot { get; private set; }

    public IReadOnlyCollection<OdometerReading> OdometerReadings => _odometerReadings;

    /// <summary>When the most recent reading was taken. <c>null</c> for a vehicle with no history.</summary>
    public DateTimeOffset? LastReadingAt { get; private set; }

    /// <summary>Adds a vehicle to the fleet, rejecting an unusable plate.</summary>
    public static Result<Vehicle> Register(
        Guid id,
        string plate,
        VehicleType type,
        Guid depotId,
        int odometerKm)
    {
        var normalisedPlate = NormalisePlate(plate);

        if (normalisedPlate.Length == 0)
        {
            return Error.Validation("vehicle.plate_required", "A vehicle needs a registration plate.");
        }

        if (normalisedPlate.Length > PlateMaxLength)
        {
            return Error.Validation(
                "vehicle.plate_too_long",
                $"A registration plate is at most {PlateMaxLength} characters.");
        }

        if (odometerKm < 0)
        {
            return Error.Validation("vehicle.odometer_negative", "Mileage cannot be negative.");
        }

        if (!Enum.IsDefined(type))
        {
            return Error.Validation("vehicle.type_unknown", $"'{type}' is not a vehicle type.");
        }

        return new Vehicle(id, normalisedPlate, type, depotId, odometerKm);
    }

    public const int PlateMaxLength = 16;

    /// <summary>
    /// Whether this vehicle may be booked. The single source of truth for the rule that Bookings
    /// asks about through <see cref="IVehicleAvailability"/>.
    /// </summary>
    public bool IsBookable => Status == VehicleStatus.Available;

    public Result ChangeStatus(VehicleStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            return Error.Validation("vehicle.status_unknown", $"'{status}' is not a vehicle status.");
        }

        if (Status == VehicleStatus.Retired && status != VehicleStatus.Retired)
        {
            // Retirement is one-way. If a vehicle really does come back, it comes back as a new
            // row with its own history, rather than quietly reusing the old one's bookings.
            return Error.Conflict(
                "vehicle.retired",
                "A retired vehicle cannot return to service.");
        }

        Status = status;
        return Result.Success();
    }

    /// <summary>
    /// Appends a reading and moves the vehicle's current mileage up to match.
    /// </summary>
    /// <remarks>
    /// Both directions of "monotonic" are checked. Mileage may not decrease, and a reading may not
    /// be dated before the last one - a reading taken last Tuesday and entered today is honest
    /// history, but it cannot be used to lower the odometer.
    /// </remarks>
    public Result<OdometerReading> RecordOdometerReading(Guid readingId, DateTimeOffset recordedAt, int km)
    {
        if (km < 0)
        {
            return Error.Validation("odometer.negative", "Mileage cannot be negative.");
        }

        if (km < OdometerKm)
        {
            return Error.Validation(
                "odometer.not_monotonic",
                $"Mileage cannot decrease: the vehicle is already at {OdometerKm} km.");
        }

        if (LastReadingAt is { } last && recordedAt < last)
        {
            return Error.Validation(
                "odometer.out_of_order",
                $"A reading cannot predate the last one, taken at {last:O}.");
        }

        var reading = new OdometerReading(readingId, Id, recordedAt, km);
        _odometerReadings.Add(reading);

        OdometerKm = km;
        LastReadingAt = recordedAt;

        return reading;
    }

    private static string NormalisePlate(string? plate) =>
        plate?.Trim().ToUpperInvariant() ?? string.Empty;
}

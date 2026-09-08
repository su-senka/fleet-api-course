using Fleet.Common.Seeding;
using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Domain;

namespace Fleet.Modules.Vehicles.Seeding;

/// <summary>
/// Builds the Vehicles seed data in memory. Pure: same input, same rows, every time.
/// </summary>
/// <remarks>
/// <para>
/// Separated from the code that writes to the database so the unit tests can assert the shape of
/// the data - 250 vehicles, monotonic readings, at least one vehicle in maintenance - without
/// needing Postgres.
/// </para>
/// <para>
/// Every id comes from <see cref="DeterministicGuid"/> and every random choice from a
/// <see cref="Random"/> constructed with a fixed seed, so a <c>make reset</c> reproduces the
/// database exactly. The dates are the one exception: they are measured backwards from an anchor
/// date so that "expires next week" keeps meaning that next year.
/// </para>
/// </remarks>
internal static class VehiclesSeedData
{
    public const int DepotCount = 4;
    public const int VehicleCount = 250;
    public const int ReadingsPerVehicle = 60;

    /// <summary>Changing this changes every generated row. It is the whole reproducibility story.</summary>
    public const int RandomSeed = 20260101;

    private static readonly (string Name, string City)[] Depots =
    [
        ("Depo Praha-Malesice", "Praha"),
        ("Depo Brno-Slatina", "Brno"),
        ("Depo Ostrava-Vitkovice", "Ostrava"),
        ("Depo Plzen-Skvrnany", "Plzen"),
    ];

    // Czech plates read like "3S2 4419": a region digit, two letters, then four digits.
    private static readonly char[] PlateLetters = "ABCEHJKLMPSTUVZ".ToCharArray();

    public static IReadOnlyList<Depot> BuildDepots() =>
    [
        .. Depots.Select((depot, index) =>
            new Depot(DeterministicGuid.Create("depot", index), depot.Name, depot.City))
    ];

    /// <summary>
    /// Builds the fleet and its odometer history.
    /// </summary>
    /// <param name="anchor">
    /// "Now" for the purposes of seeding. Readings are spread over the 18 months before it.
    /// </param>
    public static IReadOnlyList<Vehicle> BuildVehicles(IReadOnlyList<Depot> depots, DateTimeOffset anchor)
    {
        ArgumentNullException.ThrowIfNull(depots);

        var random = new Random(RandomSeed);
        var vehicles = new List<Vehicle>(VehicleCount);
        var usedPlates = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < VehicleCount; index++)
        {
            var plate = NextUniquePlate(random, usedPlates);
            var depot = depots[index % depots.Count];
            var type = PickType(random);

            // The starting mileage, before any of the history below is applied.
            var startKm = random.Next(2_000, 320_000);

            var created = Vehicle.Register(
                DeterministicGuid.Create("vehicle", index),
                plate,
                type,
                depot.Id,
                startKm);

            // Register only rejects malformed input, and everything above is generated in range.
            // If this ever throws, the generator is wrong and the seed is not worth trusting.
            if (created.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed data produced an invalid vehicle at index {index}: {created.Error}");
            }

            var vehicle = created.Value;
            AddOdometerHistory(vehicle, index, startKm, anchor, random);
            ApplyStatus(vehicle, index);

            vehicles.Add(vehicle);
        }

        return vehicles;
    }

    /// <summary>
    /// Gives a vehicle a plausible run of readings: 60 of them, always increasing, spread over the
    /// 18 months before the anchor.
    /// </summary>
    private static void AddOdometerHistory(
        Vehicle vehicle,
        int vehicleIndex,
        int startKm,
        DateTimeOffset anchor,
        Random random)
    {
        var firstReadingAt = anchor.AddMonths(-18);
        var interval = TimeSpan.FromDays(548.0 / ReadingsPerVehicle);
        var km = startKm;

        for (var readingIndex = 0; readingIndex < ReadingsPerVehicle; readingIndex++)
        {
            // A little jitter so the readings do not land on a suspiciously perfect cadence, but
            // never enough to overtake the next one and break the ordering.
            var jitter = TimeSpan.FromHours(random.Next(0, 12));
            var recordedAt = firstReadingAt + (interval * readingIndex) + jitter;

            km += random.Next(150, 2_400);

            var recorded = vehicle.RecordOdometerReading(
                DeterministicGuid.Create($"reading:{vehicleIndex}", readingIndex),
                recordedAt.ToUniversalTime(),
                km);

            if (recorded.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed data produced a non-monotonic reading for vehicle {vehicleIndex}: {recorded.Error}");
            }
        }
    }

    /// <summary>
    /// Puts roughly one vehicle in eight out of service, deterministically by index rather than by
    /// the shared <see cref="Random"/>, so that the interesting vehicles are easy to find and
    /// stay put across runs.
    /// </summary>
    private static void ApplyStatus(Vehicle vehicle, int index)
    {
        var status = (index % 20) switch
        {
            // Every twentieth vehicle is retired, and two more in each twenty are in the workshop.
            0 => VehicleStatus.Retired,
            3 or 11 => VehicleStatus.InMaintenance,
            _ => VehicleStatus.Available,
        };

        if (status is VehicleStatus.Available)
        {
            return;
        }

        var changed = vehicle.ChangeStatus(status);
        if (changed.IsFailure)
        {
            throw new InvalidOperationException(
                $"Seed data could not set status {status} on vehicle {index}: {changed.Error}");
        }
    }

    private static VehicleType PickType(Random random) => random.Next(100) switch
    {
        < 40 => VehicleType.Van,
        < 70 => VehicleType.Car,
        < 90 => VehicleType.Truck,
        _ => VehicleType.Bus,
    };

    private static string NextUniquePlate(Random random, HashSet<string> used)
    {
        // 15 letters squared times 9 regions times 10,000 numbers is comfortably more than 250,
        // so this loop is not going to spin. It exists because "generate until unique" is honest
        // about the collision the unique index would otherwise catch at insert time.
        while (true)
        {
            var plate = string.Create(8, random, static (span, rng) =>
            {
                span[0] = (char)('1' + rng.Next(9));
                span[1] = PlateLetters[rng.Next(PlateLetters.Length)];
                span[2] = PlateLetters[rng.Next(PlateLetters.Length)];
                span[3] = ' ';
                for (var i = 4; i < 8; i++)
                {
                    span[i] = (char)('0' + rng.Next(10));
                }
            });

            if (used.Add(plate))
            {
                return plate;
            }
        }
    }
}

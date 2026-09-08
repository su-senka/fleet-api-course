using Fleet.Common.Seeding;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Domain;

namespace Fleet.Modules.Bookings.Seeding;

/// <summary>
/// Builds roughly 3,000 bookings across the seeded fleet, none of which overlap.
/// </summary>
/// <remarks>
/// <para>
/// Note what this class does <em>not</em> do: read the vehicles or drivers tables. It recreates
/// their ids with the same <see cref="DeterministicGuid"/> calls the other two seeders used. Two
/// modules agreeing on an id without either querying the other is the whole point of deriving ids
/// from names rather than generating them randomly.
/// </para>
/// <para>
/// Bookings are laid out per vehicle, one per time slot across 18 months, so the exclusion
/// constraint accepts every row. Exactly one pair is deliberately made to touch - see
/// <see cref="AdjacentPairVehicleIndex"/>.
/// </para>
/// </remarks>
internal static class BookingsSeedData
{
    public const int RandomSeed = 20260101;

    /// <summary>Must match <c>VehiclesSeedData.VehicleCount</c>, and is asserted to in the tests.</summary>
    public const int VehicleCount = 250;

    /// <summary>Must match <c>DriversSeedData.DriverCount</c>.</summary>
    public const int DriverCount = 60;

    public const int BookingsPerVehicle = 12;

    /// <summary>
    /// The vehicle that gets two bookings meeting exactly end-to-start, with no gap.
    /// </summary>
    /// <remarks>
    /// The edge case everyone gets wrong. A closed-interval overlap test rejects this pair; a
    /// half-open one accepts it. Having it in the seed means a student whose check is subtly wrong
    /// finds out from their own data rather than from a bug report.
    /// </remarks>
    public const int AdjacentPairVehicleIndex = 0;

    /// <summary>
    /// The vehicle that gets a cancelled booking sitting on top of a confirmed one.
    /// </summary>
    /// <remarks>
    /// Proof that the exclusion constraint is partial. If it were not, this row could not exist,
    /// and cancelling a booking would not really free the slot.
    /// </remarks>
    public const int OverlappingCancelledVehicleIndex = 1;

    private static readonly string[] Purposes =
    [
        "Rozvoz Praha - Brno", "Servisni vyjezd", "Prevoz materialu", "Stehovani kancelare",
        "Rozvoz zbozi", "Sluzebni cesta", "Odvoz odpadu", "Zasobovani skladu",
        "Preprava palet", "Vyjezd na stavbu", "Rozvoz Ostrava", "Prevoz techniky",
    ];

    /// <summary>Builds every booking. The anchor is "now"; bookings run 18 months back and 2 forward.</summary>
    public static IReadOnlyList<Booking> BuildBookings(DateTimeOffset anchor)
    {
        var random = new Random(RandomSeed);
        var bookings = new List<Booking>(VehicleCount * BookingsPerVehicle);

        for (var vehicleIndex = 0; vehicleIndex < VehicleCount; vehicleIndex++)
        {
            BuildForVehicle(bookings, vehicleIndex, anchor, random);
        }

        return bookings;
    }

    private static void BuildForVehicle(
        List<Booking> bookings,
        int vehicleIndex,
        DateTimeOffset anchor,
        Random random)
    {
        var vehicleId = DeterministicGuid.Create("vehicle", vehicleIndex);

        // The 18 months run from 16 months back to 2 months ahead, so the fleet has a history to
        // report on *and* a calendar with something in it. A window entirely in the past would
        // mean every seeded booking is Completed and nothing is ever Confirmed.
        var firstSlotStart = anchor.AddMonths(-16);
        var slotLength = TimeSpan.FromDays(548.0 / BookingsPerVehicle);

        // Each booking sits inside its own slot and cannot reach the next one: the jitter uses at
        // most 60% of the slot and the longest booking is 30 hours, well short of the remainder.
        // That is what guarantees the exclusion constraint accepts every generated row.
        var maxJitterHours = (int)(slotLength.TotalHours * 0.6);

        for (var slot = 0; slot < BookingsPerVehicle; slot++)
        {
            var driverId = DeterministicGuid.Create("driver", random.Next(DriverCount));
            var startsAt = firstSlotStart + (slotLength * slot) + TimeSpan.FromHours(random.Next(maxJitterHours));
            var endsAt = startsAt.AddHours(random.Next(2, 30));

            var booking = CreateOrThrow(
                DeterministicGuid.Create($"booking:{vehicleIndex}", slot),
                vehicleId,
                driverId,
                Purposes[(vehicleIndex + slot) % Purposes.Length],
                startsAt,
                endsAt,
                anchor);

            ApplyStatus(booking, endsAt, anchor, slot);
            bookings.Add(booking);

            // The two special cases go in the last slot, which is in the future, so they show up
            // in a "what is booked" view rather than being buried a year deep in the history.
            var isLastSlot = slot == BookingsPerVehicle - 1;

            if (isLastSlot && vehicleIndex == AdjacentPairVehicleIndex)
            {
                // The touching pair: this one starts at the exact instant the last one ended.
                var adjacent = CreateOrThrow(
                    DeterministicGuid.Create($"booking:{vehicleIndex}", 1_000),
                    vehicleId,
                    driverId,
                    "Navazujici smena - zacina presne kdyz predchozi konci",
                    endsAt,
                    endsAt.AddHours(4),
                    anchor);

                ApplyStatus(adjacent, endsAt.AddHours(4), anchor, slot);
                bookings.Add(adjacent);
            }

            if (isLastSlot && vehicleIndex == OverlappingCancelledVehicleIndex)
            {
                // A cancelled booking sitting squarely on top of the confirmed one above. Legal
                // only because the exclusion constraint ignores cancelled rows.
                var cancelled = CreateOrThrow(
                    DeterministicGuid.Create($"booking:{vehicleIndex}", 2_000),
                    vehicleId,
                    driverId,
                    "Zruseno - prekryva potvrzenou rezervaci",
                    startsAt.AddMinutes(30),
                    endsAt.AddMinutes(30),
                    anchor);

                var wasCancelled = cancelled.Cancel(anchor);
                if (wasCancelled.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Seed data could not cancel a booking: {wasCancelled.Error}");
                }

                bookings.Add(cancelled);
            }
        }
    }

    /// <summary>
    /// Marks a booking completed once its window has passed, and cancels one in twenty.
    /// </summary>
    /// <remarks>
    /// Deliberately keyed off the slot index rather than the shared RNG, so the cancelled bookings
    /// sit at predictable places and a test can find them.
    /// </remarks>
    private static void ApplyStatus(Booking booking, DateTimeOffset endsAt, DateTimeOffset anchor, int slot)
    {
        if (slot == 7)
        {
            var cancelled = booking.Cancel(anchor);
            if (cancelled.IsFailure)
            {
                throw new InvalidOperationException($"Seed data could not cancel a booking: {cancelled.Error}");
            }

            return;
        }

        if (endsAt >= anchor)
        {
            // Still to come, or happening now. Leave it confirmed.
            return;
        }

        var completed = booking.Complete();
        if (completed.IsFailure)
        {
            throw new InvalidOperationException($"Seed data could not complete a booking: {completed.Error}");
        }
    }

    private static Booking CreateOrThrow(
        Guid id,
        Guid vehicleId,
        Guid driverId,
        string purpose,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset createdAt)
    {
        var created = Booking.Create(id, vehicleId, driverId, purpose, startsAt, endsAt, createdAt);

        if (created.IsFailure)
        {
            throw new InvalidOperationException(
                $"Seed data produced an invalid booking ({startsAt:u} to {endsAt:u}): {created.Error}");
        }

        return created.Value;
    }
}

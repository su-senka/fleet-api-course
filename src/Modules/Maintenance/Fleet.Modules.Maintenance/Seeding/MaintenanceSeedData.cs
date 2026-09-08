using Fleet.Common.Seeding;
using Fleet.Modules.Maintenance.Contracts;
using Fleet.Modules.Maintenance.Domain;

namespace Fleet.Modules.Maintenance.Seeding;

/// <summary>
/// Builds 400 work orders across the seeded fleet, in every state, with part lines.
/// </summary>
/// <remarks>
/// As with bookings, the vehicle ids are recreated with <see cref="DeterministicGuid"/> rather
/// than read from the <c>vehicles</c> schema.
/// </remarks>
internal static class MaintenanceSeedData
{
    public const int WorkOrderCount = 400;
    public const int VehicleCount = 250;

    /// <summary>
    /// How much of the fleet has ever been in the workshop. Deliberately less than all of it, so
    /// that "work orders for this vehicle" can legitimately come back empty.
    /// </summary>
    public const int VehiclesWithWorkOrders = 180;
    public const int RandomSeed = 20260101;

    /// <summary>Czech-ish part numbers, so a part search has something recognisable to find.</summary>
    private static readonly (string Number, decimal Price)[] Parts =
    [
        ("BRZ-DEST-PRED", 1_240.00m),
        ("BRZ-DEST-ZAD", 1_090.00m),
        ("OLEJ-FILTR", 320.50m),
        ("VZDUCH-FILTR", 415.00m),
        ("SVICKA-SADA", 890.00m),
        ("PNEU-205-55-R16", 2_150.00m),
        ("PNEU-225-75-R16C", 3_480.00m),
        ("BATERIE-74AH", 2_990.00m),
        ("STERAC-SADA", 540.00m),
        ("SPOJKA-SADA", 8_750.00m),
        ("TLUMIC-PRED", 3_120.00m),
        ("ALTERNATOR", 6_400.00m),
    ];

    /// <summary>
    /// Builds every work order.
    /// </summary>
    /// <param name="anchor">"Now". Work orders are opened over the 18 months before it.</param>
    public static IReadOnlyList<WorkOrder> BuildWorkOrders(DateTimeOffset anchor)
    {
        var random = new Random(RandomSeed);
        var workOrders = new List<WorkOrder>(WorkOrderCount);

        for (var index = 0; index < WorkOrderCount; index++)
        {
            // Spread over the first 180 vehicles only, in steps of 7. Some collect two or three
            // work orders, and the remaining 70 vehicles have never been in the workshop at all -
            // which is what a real fleet looks like, and what makes an empty result a case worth
            // handling rather than a bug.
            var vehicleIndex = (index * 7) % VehiclesWithWorkOrders;
            var openedAt = anchor.AddDays(-random.Next(1, 540)).AddHours(random.Next(6, 18));

            var opened = WorkOrder.Open(
                DeterministicGuid.Create("work_order", index),
                DeterministicGuid.Create("vehicle", vehicleIndex),
                PickKind(random),
                openedAt);

            if (opened.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed data produced an invalid work order at index {index}: {opened.Error}");
            }

            var workOrder = opened.Value;

            AddParts(workOrder, index, random);
            ApplyStatus(workOrder, index, openedAt, random);

            workOrders.Add(workOrder);
        }

        return workOrders;
    }

    /// <summary>
    /// Gives most work orders some parts. One in six gets none, which is what makes "you cannot
    /// order nothing" a rule worth having.
    /// </summary>
    private static void AddParts(WorkOrder workOrder, int index, Random random)
    {
        if (index % 6 == 5)
        {
            return;
        }

        var lineCount = random.Next(1, 5);

        for (var line = 0; line < lineCount; line++)
        {
            var (number, price) = Parts[random.Next(Parts.Length)];

            // Adding a part that is already on the order increases its quantity rather than
            // adding a second line, so the generator does not have to avoid duplicates itself.
            var added = workOrder.AddPartLine(number, random.Next(1, 5), price);

            if (added.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed data produced an invalid part line on work order {index}: {added.Error}");
            }
        }
    }

    /// <summary>
    /// Walks each work order to a status, keyed off the index so the interesting ones stay put.
    /// </summary>
    private static void ApplyStatus(WorkOrder workOrder, int index, DateTimeOffset openedAt, Random random)
    {
        var outcome = index % 10;

        // Every second work order with parts has already been sent to the supplier, so there are
        // plenty of rows carrying a supplier order id without anybody having to call the fake.
        if (workOrder.Lines.Count > 0 && index % 2 == 0)
        {
            var recorded = workOrder.RecordSupplierOrder($"SUP-{100_000 + index:000000}");
            if (recorded.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed data could not record a supplier order on {index}: {recorded.Error}");
            }
        }

        switch (outcome)
        {
            case 0 or 1:
                // Left open, awaiting parts or otherwise.
                break;

            case 2 or 3:
                Apply(workOrder, index, order => order.StartWork());
                break;

            case 9:
                Apply(workOrder, index, order => order.Cancel(openedAt.AddDays(random.Next(1, 20))));
                break;

            default:
                Apply(workOrder, index, order => order.StartWork());
                Apply(workOrder, index, order => order.Complete(openedAt.AddDays(random.Next(1, 30))));
                break;
        }
    }

    private static void Apply(WorkOrder workOrder, int index, Func<WorkOrder, Common.Results.Result> transition)
    {
        var result = transition(workOrder);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Seed data could not move work order {index}: {result.Error}");
        }
    }

    private static WorkOrderKind PickKind(Random random) => random.Next(100) switch
    {
        < 40 => WorkOrderKind.Service,
        < 70 => WorkOrderKind.Repair,
        < 88 => WorkOrderKind.Inspection,
        _ => WorkOrderKind.Tyres,
    };
}

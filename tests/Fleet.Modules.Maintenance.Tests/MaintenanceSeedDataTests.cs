using Fleet.Common.Seeding;
using Fleet.Modules.Maintenance.Contracts;
using Fleet.Modules.Maintenance.Seeding;

namespace Fleet.Modules.Maintenance.Tests;

public sealed class MaintenanceSeedDataTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void It_produces_the_four_hundred_work_orders_the_brief_asks_for()
    {
        var workOrders = MaintenanceSeedData.BuildWorkOrders(Anchor);

        Assert.Equal(400, workOrders.Count);
    }

    [Fact]
    public void It_produces_the_same_rows_every_time()
    {
        var first = MaintenanceSeedData.BuildWorkOrders(Anchor);
        var second = MaintenanceSeedData.BuildWorkOrders(Anchor);

        Assert.Equal(
            first.Select(order => (order.Id, order.VehicleId, order.Status, order.TotalCost)),
            second.Select(order => (order.Id, order.VehicleId, order.Status, order.TotalCost)));
    }

    [Fact]
    public void It_refers_to_vehicles_the_vehicles_seeder_created()
    {
        var workOrders = MaintenanceSeedData.BuildWorkOrders(Anchor);

        var expected = Enumerable
            .Range(0, MaintenanceSeedData.VehicleCount)
            .Select(index => DeterministicGuid.Create("vehicle", index))
            .ToHashSet();

        Assert.All(workOrders, order => Assert.Contains(order.VehicleId, expected));
    }

    [Fact]
    public void Every_status_is_represented()
    {
        var byStatus = MaintenanceSeedData.BuildWorkOrders(Anchor)
            .GroupBy(order => order.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var status in Enum.GetValues<WorkOrderStatus>())
        {
            Assert.True(
                byStatus.GetValueOrDefault(status) > 0,
                $"No work order ends up {status}, so filtering by it returns nothing.");
        }
    }

    [Fact]
    public void Some_work_orders_have_no_parts_at_all()
    {
        // These are what make "you cannot order nothing" a rule with something to fire against.
        var withoutParts = MaintenanceSeedData.BuildWorkOrders(Anchor)
            .Count(order => order.Lines.Count == 0);

        Assert.True(withoutParts > 50, $"Only {withoutParts} work orders have no parts.");
    }

    [Fact]
    public void Some_work_orders_have_already_been_sent_to_the_supplier()
    {
        // So that a supplier order id is visible in the seeded data without anybody having to call
        // the fake supplier and hope it is in a good mood.
        var ordered = MaintenanceSeedData.BuildWorkOrders(Anchor)
            .Where(order => order.PartsOrdered)
            .ToList();

        Assert.True(ordered.Count > 100, $"Only {ordered.Count} work orders carry a supplier order id.");
        Assert.All(ordered, order => Assert.NotEmpty(order.SupplierOrderId!));
        Assert.All(ordered, order => Assert.NotEmpty(order.Lines));
    }

    [Fact]
    public void No_work_order_carries_a_supplier_order_without_parts()
    {
        var workOrders = MaintenanceSeedData.BuildWorkOrders(Anchor);

        Assert.DoesNotContain(workOrders, order => order.PartsOrdered && order.Lines.Count == 0);
    }

    [Fact]
    public void Costs_are_plausible_and_exact()
    {
        var workOrders = MaintenanceSeedData.BuildWorkOrders(Anchor);

        Assert.All(workOrders, order =>
        {
            Assert.True(order.TotalCost >= 0m);

            // Every seeded price has two decimal places, so every total must too. A stray third
            // decimal would mean something has been through a float on the way.
            Assert.Equal(order.TotalCost, decimal.Round(order.TotalCost, 2));
        });

        Assert.True(workOrders.Sum(order => order.TotalCost) > 100_000m);
    }

    [Fact]
    public void Work_orders_are_opened_across_eighteen_months_and_never_in_the_future()
    {
        var workOrders = MaintenanceSeedData.BuildWorkOrders(Anchor);

        var earliest = workOrders.Min(order => order.OpenedAt);
        var latest = workOrders.Max(order => order.OpenedAt);

        Assert.True(latest <= Anchor, "A work order cannot be opened in the future.");
        Assert.True(earliest >= Anchor.AddDays(-560));
        Assert.True((latest - earliest).TotalDays > 400);
    }

    [Fact]
    public void Closed_work_orders_have_a_closing_timestamp_and_open_ones_do_not()
    {
        var workOrders = MaintenanceSeedData.BuildWorkOrders(Anchor);

        Assert.All(workOrders, order =>
        {
            if (order.Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
            {
                Assert.NotNull(order.ClosedAt);
                Assert.True(order.ClosedAt >= order.OpenedAt);
            }
            else
            {
                Assert.Null(order.ClosedAt);
            }
        });
    }

    [Fact]
    public void Work_orders_are_spread_unevenly_across_the_fleet()
    {
        // Some vehicles have several and some have none, which is what a workshop actually looks
        // like - and what makes filtering by vehicle worth doing.
        var perVehicle = MaintenanceSeedData.BuildWorkOrders(Anchor)
            .GroupBy(order => order.VehicleId)
            .Select(group => group.Count())
            .ToList();

        Assert.Equal(MaintenanceSeedData.VehiclesWithWorkOrders, perVehicle.Count);
        Assert.True(
            perVehicle.Count < MaintenanceSeedData.VehicleCount,
            "Every vehicle has a work order, so an empty result is unreachable.");
        Assert.True(perVehicle.Max() > 1, "No vehicle has more than one work order.");
    }
}

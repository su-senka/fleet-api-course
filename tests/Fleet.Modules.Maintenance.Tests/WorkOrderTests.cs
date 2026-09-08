using Fleet.Common.Results;
using Fleet.Modules.Maintenance.Contracts;
using Fleet.Modules.Maintenance.Domain;

namespace Fleet.Modules.Maintenance.Tests;

/// <summary>The work order's state machine and its part lines.</summary>
public sealed class WorkOrderTests
{
    private static readonly DateTimeOffset Opened = new(2026, 4, 1, 8, 0, 0, TimeSpan.Zero);

    private static WorkOrder NewWorkOrder(WorkOrderKind kind = WorkOrderKind.Service) =>
        WorkOrder.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), kind, Opened).Value;

    private static WorkOrder WithParts()
    {
        var workOrder = NewWorkOrder();
        workOrder.AddPartLine("OLEJ-FILTR", 2, 320.50m);
        return workOrder;
    }

    [Fact]
    public void A_new_work_order_is_open_with_nothing_ordered()
    {
        var workOrder = NewWorkOrder();

        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
        Assert.True(workOrder.IsOpen);
        Assert.False(workOrder.PartsOrdered);
        Assert.Empty(workOrder.Lines);
        Assert.Equal(0m, workOrder.TotalCost);
        Assert.Null(workOrder.ClosedAt);
    }

    [Fact]
    public void Part_numbers_are_normalised()
    {
        var workOrder = NewWorkOrder();

        workOrder.AddPartLine("  olej-filtr ", 1, 320.50m);

        Assert.Equal("OLEJ-FILTR", workOrder.Lines.Single().PartNumber);
    }

    [Fact]
    public void Adding_the_same_part_twice_increases_the_quantity_rather_than_adding_a_line()
    {
        // The rule the composite primary key enforces in the database, enforced here too so the
        // caller gets a sensible answer instead of a constraint violation.
        var workOrder = NewWorkOrder();

        workOrder.AddPartLine("PNEU-205-55-R16", 2, 2_150.00m);
        workOrder.AddPartLine("PNEU-205-55-R16", 2, 2_150.00m);

        var line = Assert.Single(workOrder.Lines);
        Assert.Equal(4, line.Quantity);
        Assert.Equal(8_600.00m, workOrder.TotalCost);
    }

    [Fact]
    public void Re_adding_a_part_takes_the_newer_price()
    {
        var workOrder = NewWorkOrder();

        workOrder.AddPartLine("BATERIE-74AH", 1, 2_990.00m);
        workOrder.AddPartLine("BATERIE-74AH", 1, 3_100.00m);

        Assert.Equal(3_100.00m, workOrder.Lines.Single().UnitPrice);
        Assert.Equal(6_200.00m, workOrder.TotalCost);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_line_needs_a_positive_quantity(int quantity)
    {
        var result = NewWorkOrder().AddPartLine("OLEJ-FILTR", quantity, 320.50m);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal("part_line.quantity_not_positive", result.Error.Code);
    }

    [Fact]
    public void A_line_cannot_have_a_negative_price()
    {
        var result = NewWorkOrder().AddPartLine("OLEJ-FILTR", 1, -1m);

        Assert.True(result.IsFailure);
        Assert.Equal("part_line.price_negative", result.Error.Code);
    }

    [Fact]
    public void A_free_part_is_allowed()
    {
        // Warranty replacements really are zero. Rejecting them would be tidy and wrong.
        var result = NewWorkOrder().AddPartLine("STERAC-SADA", 1, 0m);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Totals_are_exact()
    {
        // decimal, not double. 3 x 320.50 is 961.50, not 961.5000000000001.
        var workOrder = NewWorkOrder();
        workOrder.AddPartLine("OLEJ-FILTR", 3, 320.50m);
        workOrder.AddPartLine("VZDUCH-FILTR", 1, 415.00m);

        Assert.Equal(1_376.50m, workOrder.TotalCost);
    }

    [Fact]
    public void An_order_with_no_lines_cannot_be_sent_to_the_supplier()
    {
        var result = NewWorkOrder().CanOrderParts();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal("work_order.no_parts", result.Error.Code);
    }

    [Fact]
    public void Recording_a_supplier_order_moves_it_to_awaiting_parts()
    {
        var workOrder = WithParts();

        var result = workOrder.RecordSupplierOrder("SUP-000042");

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.AwaitingParts, workOrder.Status);
        Assert.Equal("SUP-000042", workOrder.SupplierOrderId);
        Assert.True(workOrder.PartsOrdered);
    }

    [Fact]
    public void Parts_cannot_be_ordered_twice()
    {
        // The guard that stops a retry after a timeout from ordering the same parts again - at
        // least on our side of the wire.
        var workOrder = WithParts();
        workOrder.RecordSupplierOrder("SUP-000042");

        var result = workOrder.CanOrderParts();

        Assert.True(result.IsFailure);
        Assert.Equal("work_order.parts_already_ordered", result.Error.Code);
    }

    [Fact]
    public void Parts_cannot_be_added_after_the_order_has_gone()
    {
        var workOrder = WithParts();
        workOrder.RecordSupplierOrder("SUP-000042");

        var result = workOrder.AddPartLine("SVICKA-SADA", 1, 890.00m);

        Assert.True(result.IsFailure);
        Assert.Equal("work_order.parts_already_ordered", result.Error.Code);
    }

    [Fact]
    public void A_supplier_order_id_is_required()
    {
        var result = WithParts().RecordSupplierOrder("   ");

        Assert.True(result.IsFailure);
        Assert.Equal("work_order.supplier_order_id_required", result.Error.Code);
    }

    [Fact]
    public void Work_can_start_and_then_complete()
    {
        var workOrder = WithParts();

        Assert.True(workOrder.StartWork().IsSuccess);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);

        Assert.True(workOrder.Complete(Opened.AddDays(2)).IsSuccess);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(Opened.AddDays(2), workOrder.ClosedAt);
        Assert.False(workOrder.IsOpen);
    }

    [Fact]
    public void A_completed_work_order_takes_no_more_parts()
    {
        var workOrder = NewWorkOrder();
        workOrder.Complete(Opened.AddDays(1));

        var result = workOrder.AddPartLine("OLEJ-FILTR", 1, 320.50m);

        Assert.True(result.IsFailure);
        Assert.Equal("work_order.closed", result.Error.Code);
    }

    [Fact]
    public void A_completed_work_order_cannot_be_cancelled()
    {
        var workOrder = NewWorkOrder();
        workOrder.Complete(Opened.AddDays(1));

        var result = workOrder.Cancel(Opened.AddDays(2));

        Assert.True(result.IsFailure);
        Assert.Equal("work_order.already_completed", result.Error.Code);
    }

    [Fact]
    public void A_cancelled_work_order_cannot_be_started()
    {
        var workOrder = NewWorkOrder();
        workOrder.Cancel(Opened.AddDays(1));

        var result = workOrder.StartWork();

        Assert.True(result.IsFailure);
        Assert.Equal("work_order.cannot_start", result.Error.Code);
    }

    [Fact]
    public void Work_in_progress_cannot_be_started_again()
    {
        var workOrder = NewWorkOrder();
        workOrder.StartWork();

        Assert.True(workOrder.StartWork().IsFailure);
    }
}

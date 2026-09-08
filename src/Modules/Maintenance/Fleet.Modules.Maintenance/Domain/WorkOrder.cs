using Fleet.Common.Results;
using Fleet.Modules.Maintenance.Contracts;

namespace Fleet.Modules.Maintenance.Domain;

/// <summary>
/// A job in the workshop: what is wrong with a vehicle, what parts it needs, and how far along it is.
/// </summary>
internal sealed class WorkOrder
{
    private readonly List<PartOrderLine> _lines = [];

    private WorkOrder()
    {
    }

    private WorkOrder(Guid id, Guid vehicleId, WorkOrderKind kind, DateTimeOffset openedAt)
    {
        Id = id;
        VehicleId = vehicleId;
        Kind = kind;
        Status = WorkOrderStatus.Open;
        OpenedAt = openedAt;
    }

    public const int SupplierOrderIdMaxLength = 40;

    public Guid Id { get; private set; }

    /// <summary>Another module's id, held as a plain Guid with no foreign key. As everywhere.</summary>
    public Guid VehicleId { get; private set; }

    public WorkOrderKind Kind { get; private set; }

    public WorkOrderStatus Status { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    /// <summary>When it was completed or cancelled. Null while still open.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>
    /// The id the supplier gave us, or null if we have not ordered. Its presence is what stops us
    /// ordering the same parts twice.
    /// </summary>
    public string? SupplierOrderId { get; private set; }

    public IReadOnlyCollection<PartOrderLine> Lines => _lines;

    public decimal TotalCost => _lines.Sum(line => line.LineTotal);

    /// <summary>Whether the order is still being worked on, as opposed to closed.</summary>
    public bool IsOpen => Status is WorkOrderStatus.Open or WorkOrderStatus.AwaitingParts or WorkOrderStatus.InProgress;

    public bool PartsOrdered => SupplierOrderId is not null;

    public static Result<WorkOrder> Open(Guid id, Guid vehicleId, WorkOrderKind kind, DateTimeOffset openedAt)
    {
        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("work_order.kind_unknown", $"'{kind}' is not a work order kind.");
        }

        return new WorkOrder(id, vehicleId, kind, openedAt);
    }

    /// <summary>
    /// Adds parts, or increases the quantity of a line that is already there.
    /// </summary>
    /// <remarks>
    /// Refused once the parts have gone to the supplier. Amending a placed order is a real problem
    /// with a real answer, and it is not "quietly change our copy and hope".
    /// </remarks>
    public Result<PartOrderLine> AddPartLine(string partNumber, int quantity, decimal unitPrice)
    {
        if (!IsOpen)
        {
            return Error.Conflict(
                "work_order.closed",
                $"A {Status.ToString().ToLowerInvariant()} work order cannot take new parts.");
        }

        if (PartsOrdered)
        {
            return Error.Conflict(
                "work_order.parts_already_ordered",
                $"Parts were already ordered from the supplier as {SupplierOrderId}.");
        }

        var created = PartOrderLine.Create(Id, partNumber, quantity, unitPrice);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var line = created.Value;
        var existing = _lines.Find(candidate => candidate.PartNumber == line.PartNumber);

        if (existing is not null)
        {
            existing.IncreaseBy(line.Quantity, line.UnitPrice);
            return existing;
        }

        _lines.Add(line);
        return line;
    }

    /// <summary>Checks that this order is in a fit state to be sent to the supplier.</summary>
    public Result CanOrderParts()
    {
        if (!IsOpen)
        {
            return Error.Conflict(
                "work_order.closed",
                $"A {Status.ToString().ToLowerInvariant()} work order cannot order parts.");
        }

        if (PartsOrdered)
        {
            return Error.Conflict(
                "work_order.parts_already_ordered",
                $"Parts were already ordered from the supplier as {SupplierOrderId}.");
        }

        if (_lines.Count == 0)
        {
            return Error.Conflict("work_order.no_parts", "There are no parts to order.");
        }

        return Result.Success();
    }

    /// <summary>Records the supplier's order id. Called only after the supplier has confirmed.</summary>
    public Result RecordSupplierOrder(string supplierOrderId)
    {
        var canOrder = CanOrderParts();
        if (canOrder.IsFailure)
        {
            return canOrder.Error;
        }

        if (string.IsNullOrWhiteSpace(supplierOrderId))
        {
            return Error.Validation("work_order.supplier_order_id_required", "The supplier returned no order id.");
        }

        SupplierOrderId = supplierOrderId.Trim();
        Status = WorkOrderStatus.AwaitingParts;

        return Result.Success();
    }

    public Result StartWork()
    {
        if (Status is not (WorkOrderStatus.Open or WorkOrderStatus.AwaitingParts))
        {
            return Error.Conflict(
                "work_order.cannot_start",
                $"A {Status.ToString().ToLowerInvariant()} work order cannot be started.");
        }

        Status = WorkOrderStatus.InProgress;
        return Result.Success();
    }

    public Result Complete(DateTimeOffset completedAt)
    {
        if (!IsOpen)
        {
            return Error.Conflict(
                "work_order.already_closed",
                $"This work order is already {Status.ToString().ToLowerInvariant()}.");
        }

        Status = WorkOrderStatus.Completed;
        ClosedAt = completedAt;

        return Result.Success();
    }

    public Result Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == WorkOrderStatus.Completed)
        {
            return Error.Conflict("work_order.already_completed", "A completed work order cannot be cancelled.");
        }

        if (Status == WorkOrderStatus.Cancelled)
        {
            return Error.Conflict("work_order.already_cancelled", "This work order is already cancelled.");
        }

        Status = WorkOrderStatus.Cancelled;
        ClosedAt = cancelledAt;

        return Result.Success();
    }
}

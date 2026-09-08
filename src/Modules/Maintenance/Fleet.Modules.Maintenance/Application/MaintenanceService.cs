using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Common.Time;
using Fleet.Modules.Maintenance.Contracts;
using Fleet.Modules.Maintenance.Domain;
using Fleet.Modules.Maintenance.Persistence;
using Fleet.Modules.Maintenance.Supplier;
using Fleet.Modules.Vehicles.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Maintenance.Application;

/// <summary>
/// The Maintenance application service.
/// </summary>
/// <remarks>
/// The only service here that depends on something outside the process. Note the shape of
/// <see cref="OrderPartsAsync"/>: it calls the supplier <em>first</em> and only then writes. Doing
/// it the other way round - recording the order and then sending it - would leave a work order
/// claiming a supplier order id that no supplier has ever heard of.
/// </remarks>
internal sealed class MaintenanceService(
    MaintenanceDbContext dbContext,
    ISupplierClient supplierClient,
    IVehicleCatalog vehicleCatalog,
    IClock clock) : IMaintenanceService
{
    private static readonly string[] SortableFieldNames = ["openedAt"];

    public async Task<Result<PagedResult<WorkOrderDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        if (sort is not null && !SortableFieldNames.Contains(sort.Field, StringComparer.OrdinalIgnoreCase))
        {
            return Error.Validation(
                "work_order.sort_field_unknown",
                $"Cannot sort by '{sort.Field}'. Try one of: {string.Join(", ", SortableFieldNames)}.");
        }

        var query = dbContext.WorkOrders.AsNoTracking();

        if (filter.GuidTerm("vehicleId") is { } vehicleId)
        {
            query = query.Where(workOrder => workOrder.VehicleId == vehicleId);
        }

        if (filter.EnumTerm<WorkOrderStatus>("status") is { } status)
        {
            query = query.Where(workOrder => workOrder.Status == status);
        }

        if (filter.EnumTerm<WorkOrderKind>("kind") is { } kind)
        {
            query = query.Where(workOrder => workOrder.Kind == kind);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var ordered = sort?.Direction == SortDirection.Ascending
            ? query.OrderBy(workOrder => workOrder.OpenedAt).ThenBy(workOrder => workOrder.Id)
            : query.OrderByDescending(workOrder => workOrder.OpenedAt).ThenBy(workOrder => workOrder.Id);

        // The totals are computed in SQL rather than by loading every line into memory. With 400
        // work orders it would hardly matter; the habit is what matters.
        var rows = await ordered
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(workOrder => new
            {
                workOrder.Id,
                workOrder.VehicleId,
                workOrder.Kind,
                workOrder.Status,
                workOrder.OpenedAt,
                workOrder.ClosedAt,
                workOrder.SupplierOrderId,
                LineCount = workOrder.Lines.Count,
                TotalCost = workOrder.Lines.Sum(line => (decimal?)(line.Quantity * line.UnitPrice)) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        var plates = await ResolvePlatesAsync(rows.Select(row => row.VehicleId), cancellationToken);

        IReadOnlyList<WorkOrderDto> items =
        [
            .. rows.Select(row => new WorkOrderDto(
                row.Id,
                row.VehicleId,
                plates.GetValueOrDefault(row.VehicleId, UnknownPlate),
                row.Kind,
                row.Status,
                row.OpenedAt,
                row.ClosedAt,
                row.SupplierOrderId,
                row.LineCount,
                row.TotalCost))
        ];

        return new PagedResult<WorkOrderDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<WorkOrderDetailDto>> GetAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .AsNoTracking()
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == workOrderId, cancellationToken);

        return workOrder is null
            ? NotFound(workOrderId)
            : await ToDetailAsync(workOrder, cancellationToken);
    }

    public async Task<Result<WorkOrderDetailDto>> OpenAsync(
        OpenWorkOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await vehicleCatalog.ExistsAsync(command.VehicleId, cancellationToken))
        {
            return Error.NotFound("vehicle.not_found", $"There is no vehicle with id {command.VehicleId}.");
        }

        var opened = WorkOrder.Open(Guid.CreateVersion7(), command.VehicleId, command.Kind, clock.UtcNow);
        if (opened.IsFailure)
        {
            return opened.Error;
        }

        dbContext.WorkOrders.Add(opened.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(opened.Value, cancellationToken);
    }

    public async Task<Result<WorkOrderDetailDto>> AddPartLineAsync(
        Guid workOrderId,
        AddPartLineCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var workOrder = await LoadForWriteAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return NotFound(workOrderId);
        }

        var added = workOrder.AddPartLine(command.PartNumber, command.Quantity, command.UnitPrice);
        if (added.IsFailure)
        {
            return added.Error;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(workOrder, cancellationToken);
    }

    public async Task<Result<WorkOrderDetailDto>> OrderPartsAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadForWriteAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return NotFound(workOrderId);
        }

        // Check we are allowed to order before troubling the supplier. Sending an order we would
        // then refuse to record would leave parts on their way and nothing in our database.
        var canOrder = workOrder.CanOrderParts();
        if (canOrder.IsFailure)
        {
            return canOrder.Error;
        }

        var lines = workOrder.Lines
            .Select(line => new SupplierOrderLine(line.PartNumber, line.Quantity))
            .ToList();

        // The call that leaves the process. No timeout, no retry, no circuit breaker - see
        // SupplierClient, and see docs/assignments/week-12.md.
        var placed = await supplierClient.PlaceOrderAsync(workOrder.Id, lines, cancellationToken);
        if (placed.IsFailure)
        {
            // Nothing has been written, so the caller can simply try again. Which is the easy
            // half of the problem: the hard half is a supplier that timed out *after* accepting
            // the order, where retrying might order the parts twice.
            return placed.Error;
        }

        var recorded = workOrder.RecordSupplierOrder(placed.Value.OrderId);
        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(workOrder, cancellationToken);
    }

    public Task<Result<WorkOrderDetailDto>> StartWorkAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(workOrderId, workOrder => workOrder.StartWork(), cancellationToken);

    public Task<Result<WorkOrderDetailDto>> CompleteAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(workOrderId, workOrder => workOrder.Complete(clock.UtcNow), cancellationToken);

    public Task<Result<WorkOrderDetailDto>> CancelAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(workOrderId, workOrder => workOrder.Cancel(clock.UtcNow), cancellationToken);

    /// <summary>Load, apply a state change, save. The three status endpoints differ only in the middle.</summary>
    private async Task<Result<WorkOrderDetailDto>> TransitionAsync(
        Guid workOrderId,
        Func<WorkOrder, Result> transition,
        CancellationToken cancellationToken)
    {
        var workOrder = await LoadForWriteAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return NotFound(workOrderId);
        }

        var applied = transition(workOrder);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(workOrder, cancellationToken);
    }

    private Task<WorkOrder?> LoadForWriteAsync(Guid workOrderId, CancellationToken cancellationToken) =>
        dbContext.WorkOrders
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == workOrderId, cancellationToken);

    private async Task<Dictionary<Guid, string>> ResolvePlatesAsync(
        IEnumerable<Guid> vehicleIds,
        CancellationToken cancellationToken)
    {
        var vehicles = await vehicleCatalog.GetManyAsync([.. vehicleIds], cancellationToken);

        return vehicles.ToDictionary(entry => entry.Key, entry => entry.Value.Plate);
    }

    private async Task<WorkOrderDetailDto> ToDetailAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleCatalog.GetAsync(workOrder.VehicleId, cancellationToken);

        return new WorkOrderDetailDto(
            workOrder.Id,
            workOrder.VehicleId,
            vehicle?.Plate ?? UnknownPlate,
            workOrder.Kind,
            workOrder.Status,
            workOrder.OpenedAt,
            workOrder.ClosedAt,
            workOrder.SupplierOrderId,
            workOrder.TotalCost,
            [
                .. workOrder.Lines
                    .OrderBy(line => line.PartNumber, StringComparer.Ordinal)
                    .Select(line => new PartOrderLineDto(
                        line.PartNumber, line.Quantity, line.UnitPrice, line.LineTotal))
            ]);
    }

    private const string UnknownPlate = "(unknown)";

    private static Error NotFound(Guid workOrderId) =>
        Error.NotFound("work_order.not_found", $"There is no work order with id {workOrderId}.");
}

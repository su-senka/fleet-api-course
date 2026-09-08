namespace Fleet.Modules.Maintenance.Contracts;

/// <summary>A work order as it appears in a list, without its part lines.</summary>
public sealed record WorkOrderDto(
    Guid Id,
    Guid VehicleId,
    string VehiclePlate,
    WorkOrderKind Kind,
    WorkOrderStatus Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string? SupplierOrderId,
    int PartLineCount,
    decimal TotalCost);

/// <summary>A work order with its part lines. Returned when fetching one by id.</summary>
public sealed record WorkOrderDetailDto(
    Guid Id,
    Guid VehicleId,
    string VehiclePlate,
    WorkOrderKind Kind,
    WorkOrderStatus Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string? SupplierOrderId,
    decimal TotalCost,
    IReadOnlyList<PartOrderLineDto> Lines);

/// <summary>
/// One line of parts on a work order.
/// </summary>
/// <remarks>
/// A part number appears at most once per work order - ordering the same part again increases the
/// quantity rather than adding a second line. That makes the pair
/// (<c>WorkOrderId</c>, <c>PartNumber</c>) the natural primary key, and it is.
/// </remarks>
public sealed record PartOrderLineDto(
    string PartNumber,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

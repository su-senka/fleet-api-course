namespace Fleet.Modules.Maintenance.Supplier;

/// <summary>One line of the order we send upstream.</summary>
internal sealed record SupplierOrderLine(string PartNumber, int Quantity);

/// <summary>The body of <c>POST /orders</c>.</summary>
internal sealed record SupplierOrderRequest(Guid WorkOrderId, IReadOnlyList<SupplierOrderLine> Lines);

/// <summary>
/// What the supplier sends back.
/// </summary>
/// <remarks>
/// A separate type from anything in our domain on purpose. This is somebody else's schema, and it
/// will change without asking us. Keeping it at the edge means that when it does, one class
/// changes rather than the whole module.
/// </remarks>
internal sealed record SupplierOrderResponse(
    string OrderId,
    Guid WorkOrderId,
    string Status,
    DateTimeOffset ReceivedAt);

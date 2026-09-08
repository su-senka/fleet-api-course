using Fleet.Common.Results;

namespace Fleet.Modules.Maintenance.Supplier;

/// <summary>Places part orders with the external supplier.</summary>
internal interface ISupplierClient
{
    /// <summary>
    /// Sends an order upstream.
    /// </summary>
    /// <returns>
    /// The supplier's confirmation, or <see cref="ErrorKind.Unavailable"/> when it could not be
    /// reached, refused to answer, or rate-limited us. Never throws for those cases: an upstream
    /// having a bad day is an expected outcome of calling it, not an exceptional one.
    /// </returns>
    Task<Result<SupplierOrderResponse>> PlaceOrderAsync(
        Guid workOrderId,
        IReadOnlyList<SupplierOrderLine> lines,
        CancellationToken cancellationToken = default);
}

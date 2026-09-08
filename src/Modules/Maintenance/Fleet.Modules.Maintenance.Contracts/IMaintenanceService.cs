using Fleet.Common.Paging;
using Fleet.Common.Results;

namespace Fleet.Modules.Maintenance.Contracts;

/// <summary>
/// The application service behind the Maintenance endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This is the only service in the repository that talks to something outside the process, and it
/// is therefore the only one that returns <see cref="ErrorKind.Unavailable"/>. That distinction
/// matters when you map it: a 409 says "you cannot do that", a 503 says "try again shortly", and
/// only one of them should carry a <c>Retry-After</c> header.
/// </para>
/// <para>
/// <see cref="OrderPartsAsync"/> calls a supplier that fails a quarter of the time and stalls for
/// eight seconds a sixth of the time, through an <c>HttpClient</c> with <em>no</em> timeout, no
/// retry and no circuit breaker. That is not an oversight - it is the week-12 assignment. Until
/// you fix it, one slow supplier will happily tie up every thread in your API.
/// </para>
/// </remarks>
public interface IMaintenanceService
{
    /// <summary>
    /// One page of work orders.
    /// </summary>
    /// <param name="filter">Understands <c>vehicleId</c>, <c>status</c> and <c>kind</c>.</param>
    /// <param name="sort">Understands <c>openedAt</c>.</param>
    Task<Result<PagedResult<WorkOrderDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>One work order with its part lines.</summary>
    Task<Result<WorkOrderDetailDto>> GetAsync(Guid workOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises a work order. <see cref="ErrorKind.NotFound"/> when the vehicle does not exist.
    /// </summary>
    /// <remarks>
    /// Note what this does <em>not</em> do: it does not move the vehicle to
    /// <c>InMaintenance</c>. Vehicles owns that field, and a cross-module state change travels as
    /// an integration event rather than a direct write. The plumbing for that arrives in
    /// milestone 5.
    /// </remarks>
    Task<Result<WorkOrderDetailDto>> OpenAsync(
        OpenWorkOrderCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a part line, or increases the quantity of one already on the order.
    /// </summary>
    /// <remarks>
    /// Fails with <see cref="ErrorKind.Conflict"/> once the parts have been ordered from the
    /// supplier - changing an order after it has gone is a different, harder problem.
    /// </remarks>
    Task<Result<WorkOrderDetailDto>> AddPartLineAsync(
        Guid workOrderId,
        AddPartLineCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the part lines to the supplier and records the order id it hands back.
    /// </summary>
    /// <remarks>
    /// The one call that leaves the process. Returns <see cref="ErrorKind.Unavailable"/> when the
    /// supplier is down, slow past the caller's patience, or rate-limiting us;
    /// <see cref="ErrorKind.Conflict"/> when the order has no lines or the parts have already been
    /// ordered.
    /// </remarks>
    Task<Result<WorkOrderDetailDto>> OrderPartsAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default);

    /// <summary>Moves the work order to <see cref="WorkOrderStatus.InProgress"/>.</summary>
    Task<Result<WorkOrderDetailDto>> StartWorkAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default);

    Task<Result<WorkOrderDetailDto>> CompleteAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default);

    Task<Result<WorkOrderDetailDto>> CancelAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default);
}

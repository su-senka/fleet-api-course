using Fleet.Common.Paging;
using Fleet.Common.Results;

namespace Fleet.Modules.Vehicles.Contracts;

/// <summary>
/// The application service behind the Vehicles endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This is what your endpoints call. Every method is finished and unit-tested; none of them knows
/// what HTTP is. They return <see cref="Result{T}"/>, and turning an <see cref="ErrorKind"/> into
/// a status code and a <c>ProblemDetails</c> body is the endpoint's job.
/// </para>
/// <para>
/// The mapping is not as mechanical as it looks. <see cref="ErrorKind.Conflict"/> from
/// <see cref="RecordOdometerAsync"/> is a 409, but <see cref="ErrorKind.Validation"/> could
/// defensibly be a 400 or a 422 depending on the API style you choose. Choose deliberately.
/// </para>
/// </remarks>
public interface IVehicleService
{
    /// <summary>
    /// One page of vehicles.
    /// </summary>
    /// <param name="filter">
    /// Understands <c>status</c>, <c>type</c> and <c>depotId</c> terms, plus a free-text
    /// <see cref="FilterRequest.Search"/> matched against the plate. Unknown terms are ignored.
    /// </param>
    /// <param name="sort">
    /// Understands <c>plate</c>, <c>odometerKm</c>, <c>status</c> and <c>type</c>. Anything else
    /// is a <see cref="ErrorKind.Validation"/> failure rather than a silently different order -
    /// a client that sorts by a field you do not support should be told so.
    /// </param>
    Task<Result<PagedResult<VehicleDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>One vehicle, with its depot. <see cref="ErrorKind.NotFound"/> when the id is unknown.</summary>
    Task<Result<VehicleDetailDto>> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a vehicle. Fails with <see cref="ErrorKind.Conflict"/> when the plate is already in
    /// the fleet, and <see cref="ErrorKind.NotFound"/> when the depot does not exist.
    /// </summary>
    Task<Result<VehicleDetailDto>> RegisterAsync(
        RegisterVehicleCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a vehicle to a new status. Retired vehicles cannot come back.</summary>
    Task<Result<VehicleDetailDto>> ChangeStatusAsync(
        Guid vehicleId,
        VehicleStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an odometer reading and updates the vehicle's current mileage.
    /// </summary>
    /// <remarks>
    /// Fails with <see cref="ErrorKind.Validation"/> when the reading would move the odometer
    /// backwards or predates the last reading. Odometers do not run in reverse, and a client that
    /// thinks otherwise has a bug worth telling it about.
    /// </remarks>
    Task<Result<OdometerReadingDto>> RecordOdometerAsync(
        Guid vehicleId,
        RecordOdometerCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>A vehicle's odometer history, newest first.</summary>
    Task<Result<PagedResult<OdometerReadingDto>>> ListOdometerReadingsAsync(
        Guid vehicleId,
        PageRequest page,
        CancellationToken cancellationToken = default);
}

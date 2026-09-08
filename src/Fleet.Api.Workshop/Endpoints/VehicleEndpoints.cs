namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Vehicles: the first resource with enough in it to be interesting.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IVehicleService</c> in <c>Fleet.Modules.Vehicles.Contracts</c>:
/// listing with paging, filtering and sorting, fetching one, registering one, changing status,
/// and the odometer history. All of it is finished and tested. None of it knows what HTTP is.
/// </para>
/// <para>
/// The seed has 250 vehicles, which is enough that a list endpoint returning all of them is
/// visibly the wrong answer. <c>PageRequest</c>, <c>SortRequest</c> and <c>FilterRequest</c> in
/// <c>Fleet.Common.Paging</c> are what the service expects; reading them out of a query string is
/// yours to write.
/// </para>
/// <para>
/// Worth deciding deliberately: <c>ListAsync</c> returns a validation failure when asked to sort
/// by a field it does not support. Is that a 400? Could it be a 422? Could you defensibly ignore
/// it and sort by the default instead - and what would that cost the client that asked?
/// </para>
/// </remarks>
internal static class VehicleEndpoints
{
    // TODO(week-2): map GET /vehicles and GET /vehicles/{vehicleId}.
    // TODO(week-3): add paging, filtering and sorting to the list.
    // TODO(week-4): add POST /vehicles and PUT /vehicles/{vehicleId}/status.
    //
    // See docs/assignments/week-2.md, week-3.md and week-4.md.
    // The finished version is src/Fleet.Api/Endpoints/VehicleEndpoints.cs.
}

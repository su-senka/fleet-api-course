using Fleet.Api.Workshop.Http;
using Fleet.Common.Paging;
using Fleet.Modules.Vehicles.Contracts;

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
    // TODO(week-4): add POST /vehicles and PUT /vehicles/{vehicleId}/status.

    /// <summary>
    /// The stable code <c>VehicleQueries.ApplySort</c> reports when <c>sort</c> names a field the
    /// service does not recognize. Everything else this endpoint can fail with stays on the shared
    /// <see cref="ProblemResults" /> mapping (400); this one code is deliberately elevated to 422 -
    /// the request is well-formed, it just cannot be carried out as asked.
    /// </summary>
    private const string _unsupportedSortFieldCode = "vehicle.sort_field_unknown";

    public static void MapVehicleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/vehicles").WithTags("Vehicles");

        group.MapGet("/", ListAsync)
            .WithName("ListVehicles")
            .WithSummary("Every vehicle")
            .Produces<PagedResult<VehicleDto>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{vehicleId:guid}", GetAsync)
            .WithName("GetVehicle")
            .WithSummary("One vehicle")
            .Produces<VehicleDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(IVehicleService vehicles, HttpContext http)
    {
        var result = await vehicles.ListAsync(
            http.Request.ReadPage(),
            http.Request.ReadSort(),
            http.Request.ReadFilter(),
            http.RequestAborted);

        return result.Match(
            Results.Ok,
            error => error.Code == _unsupportedSortFieldCode
                ? ProblemResults.From(error, http, StatusCodes.Status422UnprocessableEntity)
                : ProblemResults.From(error, http));
    }

    private static async Task<IResult> GetAsync(Guid vehicleId, IVehicleService vehicles, HttpContext http)
    {
        var result = await vehicles.GetAsync(vehicleId, http.RequestAborted);

        return result.Match(http, Results.Ok);
    }
}

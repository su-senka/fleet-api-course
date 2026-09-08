using Fleet.Api.Auth;
using Fleet.Api.Http;
using Fleet.Api.Requests;
using Fleet.Common.Paging;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Api.Endpoints;

/// <summary>
/// The Vehicles endpoints: the gentler of the two worked examples.
/// </summary>
/// <remarks>
/// <para>
/// One static class per resource, grouped with <c>MapGroup</c>, which is where the shared route
/// prefix, the shared authorization policy and the shared OpenAPI tag live. Setting any of those
/// once on the group rather than on five endpoints is most of the reason groups exist.
/// </para>
/// <para>
/// Every handler has the same shape: read the request, call the module, map the
/// <see cref="Common.Results.Result{T}"/> to a response. There is no business logic here and
/// there must not be - the moment an endpoint decides something, the workshop host and the
/// reference host start behaving differently.
/// </para>
/// </remarks>
internal static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/vehicles")
            .WithTags("Vehicles")
            .RequireAuthorization(FleetPolicies.AnyFleetUser);

        group.MapGet("/", ListAsync)
            .WithName("ListVehicles")
            .WithSummary("List vehicles")
            .WithDescription(
                "Paged, filterable and sortable. Filter with status, type or depotId; search "
                + "plates with q; sort by plate, odometerKm, status or type, prefixed with - for "
                + "descending. Cached for 30 seconds.")
            .Produces<PagedResult<VehicleDto>>()
            // The one cached endpoint in the API. See VehicleListCachePolicy for why this is safe
            // here and would not be on /bookings.
            .CacheOutput(VehicleListCachePolicy.Instance);

        group.MapGet("/{vehicleId:guid}", GetAsync)
            .WithName("GetVehicle")
            .WithSummary("Get one vehicle")
            .Produces<VehicleDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", RegisterAsync)
            .WithName("RegisterVehicle")
            .WithSummary("Add a vehicle to the fleet")
            .WithValidation<RegisterVehicleRequest>()
            .Produces<VehicleDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            // Adding a vehicle is not something a driver does.
            .RequireAuthorization(FleetPolicies.ManageFleet);

        group.MapPut("/{vehicleId:guid}/status", ChangeStatusAsync)
            .WithName("ChangeVehicleStatus")
            .WithSummary("Move a vehicle in or out of service")
            .WithDescription(
                "A PUT to a sub-resource rather than a PATCH on the vehicle: the status is the "
                + "whole of what is being replaced, and PUT says so without needing a patch format.")
            .WithValidation<ChangeVehicleStatusRequest>()
            .Produces<VehicleDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(FleetPolicies.ManageFleet);

        group.MapGet("/{vehicleId:guid}/odometer-readings", ListOdometerReadingsAsync)
            .WithName("ListOdometerReadings")
            .WithSummary("A vehicle's odometer history, newest first")
            .Produces<PagedResult<OdometerReadingDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{vehicleId:guid}/odometer-readings", RecordOdometerAsync)
            .WithName("RecordOdometerReading")
            .WithSummary("Record a new odometer reading")
            .WithDescription("Readings only ever go up. A lower one is rejected with a 400.")
            .WithValidation<RecordOdometerRequest>()
            .Produces<OdometerReadingDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routes;
    }

    /// <summary>Depots are a separate resource, small enough not to be paged.</summary>
    public static IEndpointRouteBuilder MapDepotEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/depots")
            .WithTags("Depots")
            .RequireAuthorization(FleetPolicies.AnyFleetUser);

        group.MapGet("/", async (IDepotService depots, HttpContext http) =>
                (await depots.ListAsync(http.RequestAborted))
                    .Match(http, Results.Ok))
            .WithName("ListDepots")
            .WithSummary("Every depot")
            .Produces<IReadOnlyList<DepotDto>>();

        group.MapGet("/{depotId:guid}", async (Guid depotId, IDepotService depots, HttpContext http) =>
                (await depots.GetAsync(depotId, http.RequestAborted))
                    .Match(http, Results.Ok))
            .WithName("GetDepot")
            .WithSummary("One depot")
            .Produces<DepotDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routes;
    }

    private static async Task<IResult> ListAsync(IVehicleService vehicles, HttpContext http)
    {
        // Paging, sorting and filtering all come out of the query string the same way on every
        // collection endpoint. See PagingParameters.
        var result = await vehicles.ListAsync(
            http.Request.ReadPage(),
            http.Request.ReadSort(),
            http.Request.ReadFilter(),
            http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> GetAsync(Guid vehicleId, IVehicleService vehicles, HttpContext http)
    {
        var result = await vehicles.GetAsync(vehicleId, http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterVehicleRequest request,
        IVehicleService vehicles,
        HttpContext http)
    {
        // The wire shape is mapped to the module's command here, and nowhere else. Handing the
        // request object straight to the module would tie its signature to our JSON.
        var command = new RegisterVehicleCommand(
            request.Plate, request.Type, request.DepotId, request.OdometerKm);

        var result = await vehicles.RegisterAsync(command, http.RequestAborted);

        // 201 with a Location header pointing at the new resource, which is what makes this a
        // creation rather than merely a successful POST.
        return result.Match(http, vehicle => Results.CreatedAtRoute(
            "GetVehicle", new { vehicleId = vehicle.Id }, vehicle));
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid vehicleId,
        ChangeVehicleStatusRequest request,
        IVehicleService vehicles,
        HttpContext http)
    {
        var result = await vehicles.ChangeStatusAsync(vehicleId, request.Status, http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> ListOdometerReadingsAsync(
        Guid vehicleId,
        IVehicleService vehicles,
        HttpContext http)
    {
        var result = await vehicles.ListOdometerReadingsAsync(
            vehicleId, http.Request.ReadPage(), http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> RecordOdometerAsync(
        Guid vehicleId,
        RecordOdometerRequest request,
        IVehicleService vehicles,
        HttpContext http)
    {
        var command = new RecordOdometerCommand(request.RecordedAt, request.Km);

        var result = await vehicles.RecordOdometerAsync(vehicleId, command, http.RequestAborted);

        return result.Match(http, reading => Results.Created(
            $"/vehicles/{vehicleId}/odometer-readings/{reading.Id}", reading));
    }
}

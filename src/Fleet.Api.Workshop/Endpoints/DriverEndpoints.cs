using Fleet.Api.Workshop.Http;
using Fleet.Api.Workshop.Requests;
using Fleet.Common.Paging;
using Fleet.Modules.Drivers.Contracts;

namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Drivers: the first resource you create and modify rather than only read.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IDriverService</c> in <c>Fleet.Modules.Drivers.Contracts</c>.
/// </para>
/// <para>
/// The interesting part is what the service refuses. Registering a driver whose employee number
/// is already taken comes back as <c>ErrorKind.Conflict</c>; a driver with no name comes back as
/// <c>ErrorKind.Validation</c>. Those are different failures and deserve different status codes,
/// and neither of them is a 500.
/// </para>
/// <para>
/// The seed contains four drivers with no licence and five whose licence has expired. The list
/// takes a <c>hasValidLicence</c> filter, which makes them easy to find - and makes a good
/// argument for why "give me the drivers who cannot drive" is a filter rather than an endpoint.
/// </para>
/// </remarks>
internal static class DriverEndpoints
{
    // TODO(week-5): reject a malformed request before it reaches the module, and return the
    //               failures in one RFC 9457 document rather than one at a time.
    //

    /// <summary>
    /// Same trick as <see cref="VehicleEndpoints"/>: an unsupported <c>sort</c> field is a
    /// well-formed request the service still cannot carry out, so it gets 422 instead of the
    /// shared 400 mapping.
    /// </summary>
    private const string _unsupportedSortFieldCode = "driver.sort_field_unknown";

    public static void MapDriverEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/drivers").WithTags("Drivers");

        group.MapGet("/", ListAsync)
            .WithName("ListDrivers")
            .WithSummary("Every driver")
            .Produces<PagedResult<DriverDto>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{driverId:guid}", GetAsync)
            .WithName("GetDriver")
            .WithSummary("One driver")
            .Produces<DriverDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", RegisterAsync)
            .WithName("RegisterDriver")
            .WithSummary("Add a driver")
            .Produces<DriverDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListAsync(IDriverService drivers, HttpContext http)
    {
        var result = await drivers.ListAsync(
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

    private static async Task<IResult> GetAsync(Guid driverId, IDriverService drivers, HttpContext http)
    {
        var result = await drivers.GetAsync(driverId, http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterDriverRequest request,
        IDriverService drivers,
        HttpContext http)
    {
        var command = new RegisterDriverCommand(request.EmployeeNumber, request.Name, request.UserId);
        var result = await drivers.RegisterAsync(command, http.RequestAborted);

        return result.Match(http, driver => Results.CreatedAtRoute(
            "GetDriver", new { driverId = driver.Id }, driver));
    }
}

using Fleet.Api.Workshop.Http;
using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Api.Workshop.Endpoints;

internal static class DepotEndpoints
{
    public static void MapDepotEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/depots").WithTags("Depots");

        group.MapGet("/", ListAsync)
            .WithName("ListDepots")
            .WithSummary("Every depot")
            .Produces<IReadOnlyList<DepotDto>>();

        group.MapGet("/{depotId}", GetAsync)
            .WithName("GetDepot")
            .WithSummary("One depot")
            .Produces<DepotDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(IDepotService depots, HttpContext http)
    {
        var result = await depots.ListAsync(http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> GetAsync(string depotId, IDepotService depots, HttpContext http)
    {
        // Malformed id falls into our own mapping instead of matching
        if (!Guid.TryParse(depotId, out var id))
        {
            return ProblemResults.From(
                Error.Validation("depot.invalid_id", $"'{depotId}' is not a valid depot id."),
                http);
        }

        var result = await depots.GetAsync(id, http.RequestAborted);

        return result.Match(http, Results.Ok);
    }
}

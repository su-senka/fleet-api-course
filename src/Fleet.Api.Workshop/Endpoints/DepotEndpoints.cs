using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Workshop.Endpoints;

internal static class DepotEndpoints
{
    private const string ProblemTypeBase = "https://fleet.example/problems/";

    public static IEndpointRouteBuilder MapDepotEndpoints(this IEndpointRouteBuilder routes)
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

        return routes;
    }

    private static async Task<IResult> ListAsync(IDepotService depots, HttpContext http)
    {
        var result = await depots.ListAsync(http.RequestAborted);

        return result.ToResponse(http, Results.Ok);
    }

    private static async Task<IResult> GetAsync(string depotId, IDepotService depots, HttpContext http)
    {
        // Malformed id falls into our own mapping instead of matching
        if (!Guid.TryParse(depotId, out var id))
        {
            return ToProblem(
                Error.Validation("depot.invalid_id", $"'{depotId}' is not a valid depot id."),
                http);
        }

        var result = await depots.GetAsync(id, http.RequestAborted);

        return result.ToResponse(http, Results.Ok);
    }

    /// <summary>Collapses a <see cref="Result{T}"/> into a response or an RFC 9457 problem.</summary>
    private static IResult ToResponse<T>(this Result<T> result, HttpContext http, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, error => ToProblem(error, http));

    private static IResult ToProblem(Error error, HttpContext http)
    {
        var status = error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        var title = error.Kind switch
        {
            ErrorKind.NotFound => "Resource not found",
            ErrorKind.Conflict => "Conflicting request",
            ErrorKind.Validation => "Invalid request",
            ErrorKind.Forbidden => "Forbidden",
            ErrorKind.Unavailable => "Service unavailable",
            _ => "Unexpected error",
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Details.Count > 0)
        {
            extensions["errors"] = error.Details;
        }

        return Results.Problem(new ProblemDetails
        {
            Type = ProblemTypeBase + error.Code,
            Title = title,
            Status = status,
            Detail = error.Message,
            Instance = $"{http.Request.Method} {http.Request.Path}",
            Extensions = extensions,
        });
    }
}

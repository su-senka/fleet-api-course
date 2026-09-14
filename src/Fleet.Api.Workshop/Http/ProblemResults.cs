using Fleet.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Workshop.Http;

/// <summary>Turns an <see cref="Error"/> into an RFC 9457 problem response.</summary>
/// <remarks>
/// Shared by every resource so the mapping from <see cref="ErrorKind"/> to a status code lives in
/// one place instead of once per endpoint file. Compare with <c>Fleet.Api/Http/ProblemResults.cs</c>.
/// </remarks>
internal static class ProblemResults
{
    private const string _problemTypeBase = "https://fleet.example/problems/";

    public static int StatusCodeFor(ErrorKind kind) => kind switch
    {
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string TitleFor(ErrorKind kind) => kind switch
    {
        ErrorKind.NotFound => "Resource not found",
        ErrorKind.Conflict => "Conflicting request",
        ErrorKind.Validation => "Invalid request",
        ErrorKind.Forbidden => "Forbidden",
        ErrorKind.Unavailable => "Service unavailable",
        _ => "Unexpected error",
    };

    /// <param name="statusCode">
    /// Overrides the status code the <see cref="ErrorKind"/> would otherwise map to. Use sparingly:
    /// it is meant for the rare case where one specific error code needs a different status than
    /// every other error of the same kind, not as a way to route around <see cref="StatusCodeFor"/>.
    /// </param>
    public static IResult From(Error error, HttpContext httpContext, int? statusCode = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Details.Count > 0)
        {
            extensions["errors"] = error.Details;
        }

        return Results.Problem(new ProblemDetails
        {
            Type = _problemTypeBase + error.Code,
            Title = TitleFor(error.Kind),
            Status = statusCode ?? StatusCodeFor(error.Kind),
            Detail = error.Message,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
            Extensions = extensions,
        });
    }

    /// <summary>Collapses a result into a response, without ever touching <c>Value</c> unguarded.</summary>
    public static IResult Match<T>(this Result<T> result, HttpContext httpContext, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, error => From(error, httpContext));
}

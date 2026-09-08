using Fleet.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Http;

/// <summary>
/// Turns an <see cref="Error"/> into an RFC 9457 problem response.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the mapping the modules refuse to make for you.</b> An application service says
/// "this conflicts with something"; it is this file that decides that means 409. The whole reason
/// <see cref="ErrorKind"/> has five values rather than being a status code is so that the decision
/// lives here, in the HTTP layer, where it can differ per endpoint.
/// </para>
/// <para>
/// And it does differ. <see cref="ErrorKind.Conflict"/> is 409 almost everywhere, but the same
/// error from a failed <c>If-Match</c> is a 412, because the client's precondition is what failed
/// rather than the request itself. See <see cref="BookingEndpoints"/> for that case.
/// </para>
/// <para>
/// The choices below are defensible, not inevitable. <see cref="ErrorKind.Validation"/> as 400
/// rather than 422 is the one most worth arguing about: 422 says "I understood the syntax and
/// disagree with the content", which is often more accurate. Pick one and be consistent.
/// </para>
/// </remarks>
internal static class ProblemResults
{
    /// <summary>
    /// The base for the <c>type</c> URI.
    /// </summary>
    /// <remarks>
    /// RFC 9457 wants a URI that identifies the problem type, and ideally one a human can fetch
    /// for an explanation. It is an identifier first and a link second - it does not have to
    /// resolve, but a client will branch on it, so it must never change once released.
    /// </remarks>
    private const string ProblemTypeBase = "https://fleet.example/problems/";

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

    /// <summary>
    /// Builds the response for an error.
    /// </summary>
    /// <param name="statusCode">
    /// Overrides the default for the error's kind. Used where the same failure means something
    /// different over HTTP - a stale <c>If-Match</c>, for instance.
    /// </param>
    public static IResult From(Error error, HttpContext httpContext, int? statusCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(httpContext);

        var status = statusCode ?? StatusCodeFor(error.Kind);

        var extensions = new Dictionary<string, object?>
        {
            // The machine-readable code, promoted to its own field. A client should branch on
            // this rather than on the human-readable detail, which is free to be reworded.
            ["code"] = error.Code,
        };

        if (error.Details.Count > 0)
        {
            // RFC 9457 leaves per-field errors to an extension. "errors", keyed by field name, is
            // the shape ASP.NET Core's own ValidationProblemDetails uses, so clients recognise it.
            extensions["errors"] = error.Details;
        }

        return Results.Problem(new ProblemDetails
        {
            Type = ProblemTypeBase + error.Code,
            Title = TitleFor(error.Kind),
            Status = status,
            Detail = error.Message,

            // Which request this happened to. Handy in a bug report, and free.
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
            Extensions = extensions,
        });
    }

    /// <summary>
    /// Collapses a result into a response, without ever touching <c>Value</c> unguarded.
    /// </summary>
    public static IResult Match<T>(
        this Result<T> result,
        HttpContext httpContext,
        Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, error => From(error, httpContext));
}

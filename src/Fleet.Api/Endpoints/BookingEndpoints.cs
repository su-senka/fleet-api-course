using Fleet.Api.Auth;
using Fleet.Api.Http;
using Fleet.Api.Middleware;
using Fleet.Api.Requests;
using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Drivers.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;

namespace Fleet.Api.Endpoints;

/// <summary>
/// The Bookings endpoints: the harder worked example, and where most of the HTTP is.
/// </summary>
/// <remarks>
/// Four things happen here that do not happen anywhere else, and each is worth reading on its own:
/// <list type="number">
/// <item><description>ETags and <c>If-Match</c>, so two dispatchers editing one booking cannot
/// silently overwrite each other.</description></item>
/// <item><description><c>Idempotency-Key</c> on the create, so a retried request does not book the
/// van twice.</description></item>
/// <item><description>Resource-based authorization, so a driver sees only their own
/// bookings.</description></item>
/// <item><description>The same <see cref="ErrorKind.Conflict"/> mapping to 409 or 412 depending
/// on why it happened.</description></item>
/// </list>
/// </remarks>
internal static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/bookings")
            .WithTags("Bookings")
            .RequireAuthorization(FleetPolicies.AnyFleetUser);

        group.MapGet("/", ListAsync)
            .WithName("ListBookings")
            .WithSummary("List bookings")
            .WithDescription(
                "Paged, filterable and sortable. Filter with vehicleId, driverId, status, and "
                + "from/to as ISO-8601 instants. A driver only ever sees their own bookings, "
                + "whatever they ask for. Deliberately not cached.")
            .Produces<PagedResult<BookingDto>>();

        group.MapGet("/{bookingId:guid}", GetAsync)
            .WithName("GetBooking")
            .WithSummary("Get one booking")
            .WithDescription(
                "Returns an ETag. Send it back in If-Match when rescheduling or cancelling.")
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", BookAsync)
            .WithName("CreateBooking")
            .WithSummary("Book a vehicle")
            .WithDescription(
                "Honours the Idempotency-Key header: repeat a request with the same key and the "
                + "original response is replayed rather than the booking being made twice.")
            .WithValidation<CreateBookingRequest>()
            .WithIdempotency()
            .Produces<BookingDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{bookingId:guid}/schedule", RescheduleAsync)
            .WithName("RescheduleBooking")
            .WithSummary("Move a booking to a new window")
            .WithDescription(
                "Requires If-Match with the booking's current ETag. 428 if the header is missing, "
                + "412 if it is stale.")
            .WithValidation<RescheduleBookingRequest>()
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{bookingId:guid}", CancelAsync)
            .WithName("CancelBooking")
            .WithSummary("Cancel a booking")
            .WithDescription(
                "Frees the vehicle's window immediately. Requires If-Match. Cancelling an already "
                + "cancelled booking answers 204, because deleting something twice is not an error "
                + "over HTTP even though the domain calls it a conflict.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return routes;
    }

    private static async Task<IResult> ListAsync(
        IBookingService bookings,
        IDriverDirectory drivers,
        HttpContext http)
    {
        var filter = http.Request.ReadFilter();

        // A driver sees only their own bookings. Note that this is done by *pinning the filter*
        // before the query runs, not by fetching everything and discarding rows afterwards -
        // which would still leak the true total count through the pagination metadata.
        var ownDriverId = await http.User.OwnDriverIdAsync(drivers, http.RequestAborted);

        if (ownDriverId is { } driverId)
        {
            var terms = new Dictionary<string, string>(filter.Terms, StringComparer.OrdinalIgnoreCase)
            {
                ["driverId"] = driverId.ToString(),
            };

            filter = new FilterRequest(filter.Search, terms);
        }

        var result = await bookings.ListAsync(
            http.Request.ReadPage(),
            http.Request.ReadSort(),
            filter,
            http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> GetAsync(
        Guid bookingId,
        IBookingService bookings,
        IAuthorizationService authorization,
        HttpContext http)
    {
        var result = await bookings.GetAsync(bookingId, http.RequestAborted);

        if (result.IsFailure)
        {
            return ProblemResults.From(result.Error, http);
        }

        var allowed = await authorization.AuthorizeAsync(
            http.User, result.Value, BookingAccessRequirement.Instance);

        if (!allowed.Succeeded)
        {
            // 403 rather than 404. Both are defensible: a 404 hides the booking's existence, which
            // matters when an id is guessable and the mere fact of it is sensitive. Booking ids
            // here are version-7 GUIDs and the fleet is one company, so honesty wins.
            return ProblemResults.From(
                Error.Forbidden("booking.not_yours", "That booking belongs to another driver."), http);
        }

        SetETag(http.Response, result.Value.RowVersion);

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> BookAsync(
        CreateBookingRequest request,
        IBookingService bookings,
        IDriverDirectory drivers,
        HttpContext http)
    {
        // A driver may book for themselves and nobody else. Dispatchers and admins may book for
        // anyone, which is why OwnDriverIdAsync returns null for them.
        var ownDriverId = await http.User.OwnDriverIdAsync(drivers, http.RequestAborted);

        if (ownDriverId is { } driverId && driverId != request.DriverId)
        {
            return ProblemResults.From(
                Error.Forbidden(
                    "booking.driver_not_self", "A driver may only book a vehicle for themselves."),
                http);
        }

        var command = new CreateBookingCommand(
            request.VehicleId, request.DriverId, request.Purpose, request.StartsAt, request.EndsAt);

        var result = await bookings.BookAsync(command, http.RequestAborted);

        if (result.IsFailure)
        {
            return ProblemResults.From(result.Error, http);
        }

        SetETag(http.Response, result.Value.RowVersion);

        return Results.CreatedAtRoute("GetBooking", new { bookingId = result.Value.Id }, result.Value);
    }

    private static async Task<IResult> RescheduleAsync(
        Guid bookingId,
        RescheduleBookingRequest request,
        IBookingService bookings,
        IAuthorizationService authorization,
        HttpContext http)
    {
        var precondition = ReadIfMatch(http);
        if (precondition.Missing)
        {
            return PreconditionRequired(http);
        }

        var access = await AuthorizeBookingAsync(bookingId, bookings, authorization, http);
        if (access is not null)
        {
            return access;
        }

        var command = new RescheduleBookingCommand(request.StartsAt, request.EndsAt, precondition.ETag);

        var result = await bookings.RescheduleAsync(bookingId, command, http.RequestAborted);

        if (result.IsFailure)
        {
            return FromBookingError(result.Error, http, preconditionSupplied: true);
        }

        SetETag(http.Response, result.Value.RowVersion);

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CancelAsync(
        Guid bookingId,
        IBookingService bookings,
        IAuthorizationService authorization,
        HttpContext http)
    {
        var precondition = ReadIfMatch(http);
        if (precondition.Missing)
        {
            return PreconditionRequired(http);
        }

        var access = await AuthorizeBookingAsync(bookingId, bookings, authorization, http);
        if (access is not null)
        {
            return access;
        }

        var result = await bookings.CancelAsync(
            bookingId, new CancelBookingCommand(precondition.ETag), http.RequestAborted);

        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        // The service calls a second cancel a Conflict, because the domain models a state machine
        // and that transition does not exist. Over HTTP, DELETE is supposed to be idempotent: the
        // booking is cancelled, which is what the caller wanted, so 204 is the kinder answer.
        // This is the clearest example in the repository of one ErrorKind meaning two things.
        if (result.Error.Code == "booking.already_cancelled")
        {
            return Results.NoContent();
        }

        return FromBookingError(result.Error, http, preconditionSupplied: true);
    }

    /// <summary>Loads the booking only to authorize it, returning a problem when the caller may not.</summary>
    private static async Task<IResult?> AuthorizeBookingAsync(
        Guid bookingId,
        IBookingService bookings,
        IAuthorizationService authorization,
        HttpContext http)
    {
        var existing = await bookings.GetAsync(bookingId, http.RequestAborted);

        if (existing.IsFailure)
        {
            return ProblemResults.From(existing.Error, http);
        }

        var allowed = await authorization.AuthorizeAsync(
            http.User, existing.Value, BookingAccessRequirement.Instance);

        return allowed.Succeeded
            ? null
            : ProblemResults.From(
                Error.Forbidden("booking.not_yours", "That booking belongs to another driver."), http);
    }

    /// <summary>
    /// Maps a booking failure, promoting a stale version to 412.
    /// </summary>
    /// <remarks>
    /// The same <see cref="ErrorKind.Conflict"/> that means 409 for an overlapping window means
    /// 412 when the client's <c>If-Match</c> was out of date - because what failed is the
    /// precondition the client attached, not the request itself. Getting this right is the
    /// difference between a client that knows to re-read and retry and one that gives up.
    /// </remarks>
    private static IResult FromBookingError(Error error, HttpContext http, bool preconditionSupplied)
    {
        var isPreconditionFailure =
            preconditionSupplied
            && error.Code is "booking.version_mismatch" or "booking.version_malformed";

        return ProblemResults.From(
            error,
            http,
            isPreconditionFailure ? StatusCodes.Status412PreconditionFailed : null);
    }

    private static IResult PreconditionRequired(HttpContext http) =>
        ProblemResults.From(
            Error.Conflict(
                "booking.if_match_required",
                "Send the booking's current ETag in an If-Match header. GET the booking to obtain one."),
            http,
            StatusCodes.Status428PreconditionRequired);

    private static (bool Missing, string? ETag) ReadIfMatch(HttpContext http)
    {
        var value = http.Request.Headers[HeaderNames.IfMatch].ToString();

        return string.IsNullOrWhiteSpace(value) ? (true, null) : (false, value);
    }

    /// <summary>
    /// Puts the booking's row version on the response as a strong ETag.
    /// </summary>
    /// <remarks>
    /// Strong, not weak: it identifies this exact revision of the booking, which is precisely what
    /// <c>If-Match</c> needs. A weak ETag would say "semantically equivalent", and equivalence is
    /// not good enough to decide whether an update is safe.
    /// </remarks>
    private static void SetETag(HttpResponse response, string rowVersion) =>
        response.Headers.ETag = $"\"{rowVersion}\"";
}

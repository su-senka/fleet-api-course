namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Bookings: the resource the second half of the course is built on.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IBookingService</c> in <c>Fleet.Modules.Bookings.Contracts</c>. It
/// returns every <see cref="Fleet.Common.Results.ErrorKind"/> the shared kernel defines except
/// <c>Unavailable</c>, which makes it the best place in the repository to think hard about status
/// codes.
/// </para>
/// <para>
/// Four separate weeks land on this one file, and each adds something that is not about routing:
/// who may see a booking, how two dispatchers avoid overwriting each other, and what happens when
/// a client retries a request whose response it never received.
/// </para>
/// <para>
/// A question worth settling early, because it decides the shape of everything after it: when a
/// driver asks for a booking that belongs to someone else, is the honest answer 403 or 404? Both
/// are defensible and they leak different things.
/// </para>
/// </remarks>
internal static class BookingEndpoints
{
    // TODO(week-7): map the collection and the single booking, behind authentication.
    // TODO(week-8): a driver must see only their own - both for one booking and for the list.
    //               These are two different problems and they need two different mechanisms.
    // TODO(week-9): return an ETag, and require If-Match on updates. Decide what a missing
    //               header means and what a stale one means; they are not the same status code.
    // TODO(week-10): honour Idempotency-Key on the create.
    //
    // See docs/assignments/week-7.md through week-10.md.
    // The finished version is src/Fleet.Api/Endpoints/BookingEndpoints.cs, and the pieces it
    // leans on are worth reading separately:
    //   Auth/BookingAuthorization.cs        the rule that needs the booking itself
    //   Middleware/IdempotencyMiddleware.cs why it is middleware and not a filter
    //   Http/ProblemResults.cs              where a status code is actually chosen
}

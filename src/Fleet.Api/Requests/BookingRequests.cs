namespace Fleet.Api.Requests;

/// <summary>The body of <c>POST /bookings</c>.</summary>
public sealed record CreateBookingRequest(
    Guid VehicleId,
    Guid DriverId,
    string Purpose,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

/// <summary>
/// The body of <c>PUT /bookings/{id}/schedule</c>.
/// </summary>
/// <remarks>
/// No version field. The expected version travels in the <c>If-Match</c> header, where HTTP
/// already has a place for it - putting it in the body as well would give a client two ways to
/// say the same thing and a way to contradict itself.
/// </remarks>
public sealed record RescheduleBookingRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

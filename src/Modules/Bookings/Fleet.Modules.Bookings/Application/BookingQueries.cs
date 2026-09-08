using System.Globalization;
using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Bookings.Application;

/// <summary>The filtering and sorting half of the booking list query.</summary>
internal static class BookingQueries
{
    public static readonly string[] SortableFieldNames = ["startsAt", "endsAt", "createdAt"];

    /// <summary>
    /// Applies the filter terms this module understands.
    /// </summary>
    /// <remarks>
    /// An unknown <em>term</em> is ignored - a client may send whatever it likes and the module
    /// reads what it recognises. A known term with an unreadable <em>value</em> is a different
    /// matter entirely: silently dropping <c>from=yesterday</c> would answer a request for one
    /// day's bookings with the whole calendar, and the client would never know. That is the kind
    /// of quiet wrongness that reaches production.
    /// </remarks>
    public static Result<IQueryable<Booking>> ApplyFilter(this IQueryable<Booking> query, FilterRequest filter)
    {
        if (filter.GuidTerm("vehicleId") is { } vehicleId)
        {
            query = query.Where(booking => booking.VehicleId == vehicleId);
        }

        if (filter.GuidTerm("driverId") is { } driverId)
        {
            query = query.Where(booking => booking.DriverId == driverId);
        }

        if (filter.EnumTerm<BookingStatus>("status") is { } status)
        {
            query = query.Where(booking => booking.Status == status);
        }

        // "from" and "to" bound the window, not the start instant: a booking that began last week
        // and is still running today is happening today, and a calendar that hid it would be
        // lying. This is the same overlap test the domain uses, expressed in SQL.
        var from = ReadInstant(filter, "from");
        if (from.IsFailure)
        {
            return from.Error;
        }

        if (from.Value is { } notBefore)
        {
            query = query.Where(booking => booking.EndsAt > notBefore);
        }

        var to = ReadInstant(filter, "to");
        if (to.IsFailure)
        {
            return to.Error;
        }

        if (to.Value is { } notAfter)
        {
            query = query.Where(booking => booking.StartsAt < notAfter);
        }

        if (filter.Search is { } search)
        {
            var pattern = $"%{search}%";
            query = query.Where(booking => EF.Functions.ILike(booking.Purpose, pattern));
        }

        return Result<IQueryable<Booking>>.Success(query);
    }

    public static Result<IQueryable<Booking>> ApplySort(this IQueryable<Booking> query, SortRequest? sort)
    {
        // Id is the tie-breaker throughout. Booking windows are not unique - a fleet of 250 vans
        // going out at 08:00 on Monday is normal - so ordering by StartsAt alone would let the
        // same row appear on two pages.
        if (sort is null)
        {
            return Result<IQueryable<Booking>>.Success(
                query.OrderByDescending(booking => booking.StartsAt).ThenBy(booking => booking.Id));
        }

        var descending = sort.Direction == SortDirection.Descending;

        IQueryable<Booking>? ordered = sort.Field.ToLowerInvariant() switch
        {
            "startsat" => descending
                ? query.OrderByDescending(booking => booking.StartsAt).ThenBy(booking => booking.Id)
                : query.OrderBy(booking => booking.StartsAt).ThenBy(booking => booking.Id),
            "endsat" => descending
                ? query.OrderByDescending(booking => booking.EndsAt).ThenBy(booking => booking.Id)
                : query.OrderBy(booking => booking.EndsAt).ThenBy(booking => booking.Id),
            "createdat" => descending
                ? query.OrderByDescending(booking => booking.CreatedAt).ThenBy(booking => booking.Id)
                : query.OrderBy(booking => booking.CreatedAt).ThenBy(booking => booking.Id),
            _ => null,
        };

        return ordered is null
            ? Error.Validation(
                "booking.sort_field_unknown",
                $"Cannot sort by '{sort.Field}'. Try one of: {string.Join(", ", SortableFieldNames)}.")
            : Result<IQueryable<Booking>>.Success(ordered);
    }

    /// <summary>
    /// Reads a term as an instant. Absent is fine; present and unreadable is not.
    /// </summary>
    /// <remarks>
    /// A common way to arrive here with nonsense is an un-encoded <c>+</c> in a query string:
    /// <c>?from=2026-01-01T00:00:00+00:00</c> reaches the server with the offset turned into a
    /// space. Answering that with a 400 rather than the whole calendar is the difference between
    /// a client fixing a bug and never noticing one.
    /// </remarks>
    private static Result<DateTimeOffset?> ReadInstant(FilterRequest filter, string key)
    {
        var value = filter[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<DateTimeOffset?>.Success(null);
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? Result<DateTimeOffset?>.Success(parsed)
            : Error.Validation(
                $"booking.{key}_invalid",
                $"'{value}' is not an ISO-8601 instant. Remember to URL-encode the + in an offset.");
    }
}

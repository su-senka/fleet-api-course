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

    public static IQueryable<Booking> ApplyFilter(this IQueryable<Booking> query, FilterRequest filter)
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
        if (ParseInstant(filter["from"]) is { } from)
        {
            query = query.Where(booking => booking.EndsAt > from);
        }

        if (ParseInstant(filter["to"]) is { } to)
        {
            query = query.Where(booking => booking.StartsAt < to);
        }

        if (filter.Search is { } search)
        {
            var pattern = $"%{search}%";
            query = query.Where(booking => EF.Functions.ILike(booking.Purpose, pattern));
        }

        return query;
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

    private static DateTimeOffset? ParseInstant(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
}

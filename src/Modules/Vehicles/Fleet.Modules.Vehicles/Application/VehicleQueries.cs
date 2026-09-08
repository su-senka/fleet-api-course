using System.Linq.Expressions;
using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Modules.Vehicles.Contracts;
using Fleet.Modules.Vehicles.Domain;

namespace Fleet.Modules.Vehicles.Application;

/// <summary>
/// The LINQ that turns a <see cref="FilterRequest"/> and a <see cref="SortRequest"/> into a query.
/// </summary>
/// <remarks>
/// Kept apart from the service so the service reads as a list of steps rather than a wall of
/// <c>Where</c> clauses, and so the sortable-field allow-list has an obvious home.
/// </remarks>
internal static class VehicleQueries
{
    /// <summary>
    /// The fields a client is allowed to sort by, mapped to the expression that does it.
    /// </summary>
    /// <remarks>
    /// An allow-list, not a lookup by reflection over property names. The alternative - building
    /// an expression from whatever string arrived in the query string - turns the sort parameter
    /// into a way to probe the shape of your entities, and on some dynamic-LINQ libraries into
    /// something considerably worse.
    /// </remarks>
    private static readonly Dictionary<string, Expression<Func<Vehicle, object>>> SortableFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["plate"] = vehicle => vehicle.Plate,
            ["odometerKm"] = vehicle => vehicle.OdometerKm,
            ["status"] = vehicle => vehicle.Status,
            ["type"] = vehicle => vehicle.Type,
        };

    public static IReadOnlyCollection<string> SortableFieldNames => SortableFields.Keys;

    public static IQueryable<Vehicle> ApplyFilter(this IQueryable<Vehicle> query, FilterRequest filter)
    {
        if (filter.EnumTerm<VehicleStatus>("status") is { } status)
        {
            query = query.Where(vehicle => vehicle.Status == status);
        }

        if (filter.EnumTerm<VehicleType>("type") is { } type)
        {
            query = query.Where(vehicle => vehicle.Type == type);
        }

        if (filter.GuidTerm("depotId") is { } depotId)
        {
            query = query.Where(vehicle => vehicle.DepotId == depotId);
        }

        if (filter.Search is { } search)
        {
            // Plates are stored upper case and normalised on the way in, so an exact-prefix match
            // on the upper-cased search term uses the unique index. A Contains() here would work
            // too and would scan all 250 rows - fine at this size, and a useful thing to watch in
            // Jaeger once the table is larger.
            var normalised = search.ToUpperInvariant();
            query = query.Where(vehicle => vehicle.Plate.StartsWith(normalised));
        }

        return query;
    }

    /// <summary>
    /// Applies the requested sort, or a stable default when none was asked for.
    /// </summary>
    /// <returns>
    /// A <see cref="ErrorKind.Validation"/> failure when the client asked for a field that is not
    /// on the allow-list.
    /// </returns>
    public static Result<IQueryable<Vehicle>> ApplySort(this IQueryable<Vehicle> query, SortRequest? sort)
    {
        if (sort is null)
        {
            // Paging without an ORDER BY is undefined: Postgres may return the same row on page 1
            // and page 2. Plate is unique, so it is a safe default and a stable tie-breaker.
            return Result<IQueryable<Vehicle>>.Success(query.OrderBy(vehicle => vehicle.Plate));
        }

        if (!SortableFields.TryGetValue(sort.Field, out var keySelector))
        {
            return Error.Validation(
                "vehicle.sort_field_unknown",
                $"Cannot sort by '{sort.Field}'. Try one of: {string.Join(", ", SortableFieldNames)}.");
        }

        var ordered = sort.Direction == SortDirection.Descending
            ? query.OrderByDescending(keySelector).ThenBy(vehicle => vehicle.Plate)
            : query.OrderBy(keySelector).ThenBy(vehicle => vehicle.Plate);

        return Result<IQueryable<Vehicle>>.Success(ordered);
    }
}

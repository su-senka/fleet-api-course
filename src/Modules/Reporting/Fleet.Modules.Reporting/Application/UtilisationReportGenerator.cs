using System.Globalization;
using System.Text;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Reporting.Contracts;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Modules.Reporting.Application;

/// <summary>One row of the report: a vehicle in a month.</summary>
internal sealed record UtilisationRow(
    string Plate,
    VehicleType Type,
    int Year,
    int Month,
    double BookedHours,
    double AvailableHours)
{
    public double UtilisationPercent => AvailableHours <= 0 ? 0 : BookedHours / AvailableHours * 100;
}

/// <summary>
/// Works out how busy each vehicle was, month by month, and writes it as CSV.
/// </summary>
/// <remarks>
/// <para>
/// Reads both other modules through their contracts - <see cref="IVehicleCatalog"/> for the fleet
/// and <see cref="IBookingCalendar"/> for the bookings - and joins them in memory. There is no SQL
/// join here and there could not be: the two tables live in different schemas and the boundary
/// exists precisely to stop that query being written.
/// </para>
/// <para>
/// That is a real cost, and worth being honest about. A report over a fleet of fifty thousand
/// would need a different design - a read model fed by events, most likely. At 250 vehicles and
/// 3,000 bookings it is a few milliseconds, and the clarity is worth more than the microseconds.
/// </para>
/// </remarks>
internal static class UtilisationReportGenerator
{
    /// <summary>
    /// Apportions each booking across the months it touches.
    /// </summary>
    /// <remarks>
    /// A booking from 31 January to 2 February belongs partly to each. Counting it wholly in
    /// whichever month it started would be simpler and would quietly overstate January every time.
    /// </remarks>
    public static IReadOnlyList<UtilisationRow> Build(
        IReadOnlyList<VehicleSummary> vehicles,
        IReadOnlyList<BookedPeriod> bookings,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var months = MonthsBetween(from, to).ToList();
        var bookedByVehicleMonth = new Dictionary<(Guid VehicleId, int Year, int Month), double>();

        foreach (var booking in bookings)
        {
            foreach (var (year, month) in months)
            {
                var monthStart = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
                var monthEnd = monthStart.AddMonths(1);

                // The overlap between the booking and the month, in hours. Zero when they do not
                // touch, which is most pairs.
                var overlapStart = booking.StartsAt > monthStart ? booking.StartsAt : monthStart;
                var overlapEnd = booking.EndsAt < monthEnd ? booking.EndsAt : monthEnd;

                if (overlapEnd <= overlapStart)
                {
                    continue;
                }

                var key = (booking.VehicleId, year, month);
                bookedByVehicleMonth[key] =
                    bookedByVehicleMonth.GetValueOrDefault(key) + (overlapEnd - overlapStart).TotalHours;
            }
        }

        var rows = new List<UtilisationRow>(vehicles.Count * months.Count);

        foreach (var vehicle in vehicles)
        {
            foreach (var (year, month) in months)
            {
                var availableHours = DateTime.DaysInMonth(year, month) * 24.0;
                var bookedHours = bookedByVehicleMonth.GetValueOrDefault((vehicle.Id, year, month));

                rows.Add(new UtilisationRow(
                    vehicle.Plate, vehicle.Type, year, month, bookedHours, availableHours));
            }
        }

        return rows;
    }

    public static byte[] ToCsv(IReadOnlyList<UtilisationRow> rows)
    {
        var builder = new StringBuilder();

        builder.AppendLine("plate,type,year,month,booked_hours,available_hours,utilisation_percent");

        foreach (var row in rows.OrderBy(r => r.Plate, StringComparer.Ordinal).ThenBy(r => r.Year).ThenBy(r => r.Month))
        {
            // InvariantCulture throughout: a CSV whose decimal separator depends on the server's
            // locale is a CSV that breaks the moment it is opened somewhere else.
            builder.Append(Escape(row.Plate)).Append(',')
                .Append(row.Type).Append(',')
                .Append(row.Year.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Month.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.BookedHours.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.AvailableHours.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.UtilisationPercent.ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        // UTF-8 with a BOM, because the single most likely thing to open this file is Excel, and
        // without one it mangles anything outside ASCII.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    private static string Escape(string value) =>
        value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    private static IEnumerable<(int Year, int Month)> MonthsBetween(DateTimeOffset from, DateTimeOffset to)
    {
        var cursor = new DateTimeOffset(from.Year, from.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var last = new DateTimeOffset(to.Year, to.Month, 1, 0, 0, 0, TimeSpan.Zero);

        while (cursor <= last)
        {
            yield return (cursor.Year, cursor.Month);
            cursor = cursor.AddMonths(1);
        }
    }
}

using System.Globalization;
using System.Text;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Reporting.Application;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Modules.Reporting.Tests;

/// <summary>
/// The utilisation arithmetic, with no database and no blob store in sight.
/// </summary>
/// <remarks>
/// The interesting case is a booking that straddles a month boundary. Counting it wholly in the
/// month it started would be simpler and would overstate January every single year.
/// </remarks>
public sealed class UtilisationReportGeneratorTests
{
    private static readonly Guid VanId = Guid.CreateVersion7();
    private static readonly Guid TruckId = Guid.CreateVersion7();

    private static readonly VehicleSummary Van =
        new(VanId, "1AB 2345", VehicleType.Van, VehicleStatus.Available);

    private static readonly VehicleSummary Truck =
        new(TruckId, "2CD 3456", VehicleType.Truck, VehicleStatus.Available);

    private static DateTimeOffset At(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_vehicle_with_no_bookings_shows_zero_utilisation()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van], [], At(2026, 1, 1), At(2026, 1, 1));

        var row = Assert.Single(rows);
        Assert.Equal(0, row.BookedHours);
        Assert.Equal(31 * 24, row.AvailableHours);
        Assert.Equal(0, row.UtilisationPercent);
    }

    [Fact]
    public void Booked_hours_are_counted()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van],
            [new BookedPeriod(VanId, At(2026, 1, 5, 9), At(2026, 1, 5, 17))],
            At(2026, 1, 1),
            At(2026, 1, 1));

        var row = Assert.Single(rows);
        Assert.Equal(8, row.BookedHours);
        Assert.Equal(8.0 / (31 * 24) * 100, row.UtilisationPercent, 6);
    }

    [Fact]
    public void A_booking_across_a_month_boundary_is_split_between_the_months()
    {
        // 31 January 18:00 to 2 February 06:00. Six hours belong to January and thirty to
        // February, and neither month should be credited with all thirty-six.
        var rows = UtilisationReportGenerator.Build(
            [Van],
            [new BookedPeriod(VanId, At(2026, 1, 31, 18), At(2026, 2, 2, 6))],
            At(2026, 1, 1),
            At(2026, 2, 1));

        var january = rows.Single(row => row.Month == 1);
        var february = rows.Single(row => row.Month == 2);

        Assert.Equal(6, january.BookedHours);
        Assert.Equal(30, february.BookedHours);
    }

    [Fact]
    public void A_booking_outside_the_window_is_ignored()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van],
            [new BookedPeriod(VanId, At(2025, 12, 1), At(2025, 12, 2))],
            At(2026, 1, 1),
            At(2026, 1, 1));

        Assert.Equal(0, Assert.Single(rows).BookedHours);
    }

    [Fact]
    public void A_booking_only_partly_inside_the_window_contributes_only_that_part()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van],
            [new BookedPeriod(VanId, At(2025, 12, 30), At(2026, 1, 1, 10))],
            At(2026, 1, 1),
            At(2026, 1, 1));

        Assert.Equal(10, Assert.Single(rows).BookedHours);
    }

    [Fact]
    public void Bookings_are_attributed_to_the_right_vehicle()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van, Truck],
            [new BookedPeriod(TruckId, At(2026, 1, 5, 9), At(2026, 1, 5, 13))],
            At(2026, 1, 1),
            At(2026, 1, 1));

        Assert.Equal(0, rows.Single(row => row.Plate == Van.Plate).BookedHours);
        Assert.Equal(4, rows.Single(row => row.Plate == Truck.Plate).BookedHours);
    }

    [Fact]
    public void Several_bookings_in_a_month_add_up()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van],
            [
                new BookedPeriod(VanId, At(2026, 1, 5, 9), At(2026, 1, 5, 12)),
                new BookedPeriod(VanId, At(2026, 1, 9, 8), At(2026, 1, 9, 13)),
            ],
            At(2026, 1, 1),
            At(2026, 1, 1));

        Assert.Equal(8, Assert.Single(rows).BookedHours);
    }

    [Fact]
    public void Every_vehicle_gets_a_row_for_every_month()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van, Truck], [], At(2026, 1, 1), At(2026, 3, 1));

        // Two vehicles, three months. A vehicle that was never booked still needs a row, or the
        // fleet's overall utilisation is computed over the wrong denominator.
        Assert.Equal(6, rows.Count);
    }

    [Fact]
    public void February_has_fewer_available_hours_than_January()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van], [], At(2026, 1, 1), At(2026, 2, 1));

        Assert.Equal(31 * 24, rows.Single(row => row.Month == 1).AvailableHours);
        Assert.Equal(28 * 24, rows.Single(row => row.Month == 2).AvailableHours);
    }

    [Fact]
    public void A_leap_year_february_has_twenty_nine_days()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van], [], At(2028, 2, 1), At(2028, 2, 1));

        Assert.Equal(29 * 24, Assert.Single(rows).AvailableHours);
    }

    [Fact]
    public void The_csv_has_a_header_and_a_row_per_vehicle_month()
    {
        var rows = UtilisationReportGenerator.Build(
            [Van, Truck],
            [new BookedPeriod(VanId, At(2026, 1, 5, 9), At(2026, 1, 5, 12))],
            At(2026, 1, 1),
            At(2026, 1, 1));

        var csv = Encoding.UTF8.GetString(UtilisationReportGenerator.ToCsv(rows));
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Contains("plate,type,year,month,booked_hours,available_hours,utilisation_percent", lines[0], StringComparison.Ordinal);
        Assert.Contains("1AB 2345,Van,2026,1,3.00", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_csv_uses_invariant_numbers_whatever_the_machine_thinks()
    {
        // A CSV whose decimal separator depends on the server's locale is a CSV that breaks the
        // first time it is opened somewhere else.
        var original = Thread.CurrentThread.CurrentCulture;

        try
        {
            // Built by hand rather than by name: the repository sets InvariantGlobalization, so
            // there is no "cs-CZ" to ask for. Cloning the invariant culture and giving it a comma
            // separator reproduces the hazard exactly.
            var commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            commaCulture.NumberFormat.NumberDecimalSeparator = ",";

            Thread.CurrentThread.CurrentCulture = commaCulture;

            var rows = UtilisationReportGenerator.Build(
                [Van],
                [new BookedPeriod(VanId, At(2026, 1, 5, 9), At(2026, 1, 5, 12))],
                At(2026, 1, 1),
                At(2026, 1, 1));

            var csv = Encoding.UTF8.GetString(UtilisationReportGenerator.ToCsv(rows));

            Assert.Contains("3.00", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("3,00", csv, StringComparison.Ordinal);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void The_csv_starts_with_a_utf8_byte_order_mark()
    {
        // Excel is the most likely thing to open this, and without a BOM it mangles anything
        // outside ASCII - which Czech plates and depot names very much are.
        var csv = UtilisationReportGenerator.ToCsv(
            UtilisationReportGenerator.Build([Van], [], At(2026, 1, 1), At(2026, 1, 1)));

        Assert.Equal(Encoding.UTF8.GetPreamble(), csv[..3]);
    }
}

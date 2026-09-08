using Fleet.Modules.Bookings.Domain;

namespace Fleet.Modules.Bookings.Tests;

/// <summary>
/// The overlap rule, in isolation.
/// </summary>
/// <remarks>
/// Every one of these cases has a counterpart in the Postgres exclusion constraint, which uses
/// <c>tstzrange(starts_at, ends_at, '[)')</c>. If this class and that constraint ever disagree,
/// the database wins and the service starts returning conflicts it did not predict.
/// </remarks>
public sealed class BookingWindowTests
{
    private static readonly DateTimeOffset Nine = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static BookingWindow Window(int startHour, int endHour) =>
        new(Nine.AddHours(startHour - 9), Nine.AddHours(endHour - 9));

    [Fact]
    public void Two_windows_at_the_same_time_overlap()
    {
        Assert.True(Window(9, 12).Overlaps(Window(9, 12)));
    }

    [Fact]
    public void A_window_starting_inside_another_overlaps()
    {
        Assert.True(Window(9, 12).Overlaps(Window(11, 14)));
    }

    [Fact]
    public void A_window_entirely_inside_another_overlaps()
    {
        Assert.True(Window(9, 17).Overlaps(Window(11, 12)));
    }

    [Fact]
    public void A_window_entirely_containing_another_overlaps()
    {
        Assert.True(Window(11, 12).Overlaps(Window(9, 17)));
    }

    [Fact]
    public void Touching_windows_do_not_overlap()
    {
        // The case the seed data contains on purpose, and the one a closed-interval check gets
        // wrong: the van is handed over at 12:00 and the next driver takes it at 12:00.
        Assert.False(Window(9, 12).Overlaps(Window(12, 15)));
        Assert.False(Window(12, 15).Overlaps(Window(9, 12)));
    }

    [Fact]
    public void Windows_a_second_apart_do_not_overlap()
    {
        var first = new BookingWindow(Nine, Nine.AddHours(3));
        var second = new BookingWindow(Nine.AddHours(3).AddSeconds(1), Nine.AddHours(6));

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void Windows_overlapping_by_a_single_second_do_overlap()
    {
        var first = new BookingWindow(Nine, Nine.AddHours(3));
        var second = new BookingWindow(Nine.AddHours(3).AddSeconds(-1), Nine.AddHours(6));

        Assert.True(first.Overlaps(second));
    }

    [Fact]
    public void Separate_windows_do_not_overlap()
    {
        Assert.False(Window(9, 10).Overlaps(Window(14, 16)));
    }

    [Fact]
    public void Overlap_does_not_care_which_way_round_you_ask()
    {
        var morning = Window(9, 13);
        var afternoon = Window(12, 17);

        Assert.Equal(morning.Overlaps(afternoon), afternoon.Overlaps(morning));
    }

    [Fact]
    public void An_empty_window_overlaps_nothing_not_even_itself()
    {
        // A degenerate window is rejected by Booking.Create long before it reaches here, but the
        // maths should still be consistent: a half-open interval containing no instants cannot
        // share an instant with anything.
        var empty = new BookingWindow(Nine, Nine);

        Assert.True(empty.IsEmpty);
        Assert.False(empty.Overlaps(empty));
        Assert.False(empty.Overlaps(Window(9, 17)));
    }
}

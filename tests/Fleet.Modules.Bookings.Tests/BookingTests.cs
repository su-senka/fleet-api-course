using Fleet.Common.Results;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Bookings.Domain;

namespace Fleet.Modules.Bookings.Tests;

/// <summary>The booking's own rules: a valid window, and a state machine with three states.</summary>
public sealed class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);

    private static Result<Booking> Create(DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null) =>
        Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Rozvoz Praha - Brno",
            startsAt ?? Start,
            endsAt ?? Start.AddHours(6),
            Now);

    private static Booking Confirmed() => Create().Value;

    [Fact]
    public void A_new_booking_is_confirmed_and_blocks_the_vehicle()
    {
        var booking = Confirmed();

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.True(booking.BlocksVehicle);
        Assert.Null(booking.CancelledAt);
    }

    [Fact]
    public void A_booking_that_ends_before_it_starts_is_rejected()
    {
        var result = Create(Start, Start.AddHours(-1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal("booking.ends_before_it_starts", result.Error.Code);
    }

    [Fact]
    public void A_zero_length_booking_is_rejected()
    {
        var result = Create(Start, Start);

        Assert.True(result.IsFailure);
        Assert.Equal("booking.ends_before_it_starts", result.Error.Code);
    }

    [Fact]
    public void A_booking_shorter_than_the_minimum_is_rejected()
    {
        var result = Create(Start, Start.AddMinutes(5));

        Assert.True(result.IsFailure);
        Assert.Equal("booking.too_short", result.Error.Code);
    }

    [Fact]
    public void A_booking_of_exactly_the_minimum_is_accepted()
    {
        Assert.True(Create(Start, Start + Booking.MinimumDuration).IsSuccess);
    }

    [Fact]
    public void A_booking_longer_than_the_maximum_is_rejected()
    {
        // Guards against a mistyped year taking a van out of service until the next decade.
        var result = Create(Start, Start.AddYears(1));

        Assert.True(result.IsFailure);
        Assert.Equal("booking.too_long", result.Error.Code);
    }

    [Fact]
    public void A_booking_needs_a_purpose()
    {
        var result = Booking.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "   ", Start, Start.AddHours(4), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("booking.purpose_required", result.Error.Code);
    }

    [Fact]
    public void Windows_are_normalised_to_utc()
    {
        // A client in Prague sends +02:00. Everything downstream - the overlap check, the
        // exclusion constraint, the ETag - has to be comparing the same instants.
        var prague = new DateTimeOffset(2026, 6, 10, 11, 0, 0, TimeSpan.FromHours(2));

        var booking = Booking.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "Sluzebni cesta", prague, prague.AddHours(3), Now).Value;

        Assert.Equal(TimeSpan.Zero, booking.StartsAt.Offset);
        Assert.Equal(9, booking.StartsAt.Hour);
    }

    [Fact]
    public void Cancelling_frees_the_vehicle()
    {
        var booking = Confirmed();

        var result = booking.Cancel(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.False(booking.BlocksVehicle);
        Assert.Equal(Now, booking.CancelledAt);
    }

    [Fact]
    public void Cancelling_twice_is_a_conflict()
    {
        var booking = Confirmed();
        booking.Cancel(Now);

        var result = booking.Cancel(Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal("booking.already_cancelled", result.Error.Code);
    }

    [Fact]
    public void A_completed_booking_cannot_be_cancelled()
    {
        var booking = Confirmed();
        booking.Complete();

        var result = booking.Cancel(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("booking.already_completed", result.Error.Code);
    }

    [Fact]
    public void A_completed_booking_still_blocks_the_vehicle()
    {
        // It happened. The van really was unavailable, and the history has to keep saying so.
        var booking = Confirmed();
        booking.Complete();

        Assert.True(booking.BlocksVehicle);
    }

    [Fact]
    public void Rescheduling_moves_the_window()
    {
        var booking = Confirmed();

        var result = booking.Reschedule(Start.AddDays(1), Start.AddDays(1).AddHours(4));

        Assert.True(result.IsSuccess);
        Assert.Equal(Start.AddDays(1), booking.StartsAt);
    }

    [Theory]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed)]
    public void Only_a_confirmed_booking_can_be_rescheduled(BookingStatus status)
    {
        var booking = Confirmed();

        if (status == BookingStatus.Cancelled)
        {
            booking.Cancel(Now);
        }
        else
        {
            booking.Complete();
        }

        var result = booking.Reschedule(Start.AddDays(1), Start.AddDays(1).AddHours(4));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal("booking.not_confirmed", result.Error.Code);
    }

    [Fact]
    public void A_rejected_reschedule_leaves_the_window_alone()
    {
        var booking = Confirmed();
        var originalStart = booking.StartsAt;

        booking.Reschedule(Start.AddDays(1), Start.AddDays(1).AddMinutes(2));

        Assert.Equal(originalStart, booking.StartsAt);
    }
}

namespace Fleet.Modules.Bookings.Contracts;

/// <summary>Where a booking is in its short life.</summary>
public enum BookingStatus
{
    /// <summary>Booked and holding the vehicle. This is the only status that blocks the calendar.</summary>
    Confirmed = 1,

    /// <summary>Called off. Frees the slot immediately, and the row stays as history.</summary>
    Cancelled = 2,

    /// <summary>The trip happened and is over.</summary>
    Completed = 3,
}

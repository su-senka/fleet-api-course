namespace Fleet.Modules.Vehicles.Contracts;

/// <summary>
/// Where a vehicle is in its life cycle.
/// </summary>
/// <remarks>
/// Only <see cref="Available"/> can be booked. That rule lives in the Vehicles module and is
/// answered through <see cref="IVehicleAvailability"/> - Bookings must not read this enum and
/// decide for itself, or the rule ends up in two places and drifts.
/// </remarks>
public enum VehicleStatus
{
    /// <summary>In service and bookable.</summary>
    Available = 1,

    /// <summary>In the workshop. Cannot be booked, however free the calendar looks.</summary>
    InMaintenance = 2,

    /// <summary>Out of the fleet for good. Kept for the history its bookings and readings carry.</summary>
    Retired = 3,
}

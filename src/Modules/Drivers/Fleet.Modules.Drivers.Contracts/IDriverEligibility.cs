namespace Fleet.Modules.Drivers.Contracts;

/// <summary>
/// Answers whether a driver is allowed behind the wheel on a given day.
/// </summary>
/// <remarks>
/// The rule today is "holds a licence that has not expired". Bookings calls this instead of
/// reading certificates itself, so that when the rule grows a second clause - a medical, an age
/// limit, a suspension list - no caller has to change.
/// </remarks>
public interface IDriverEligibility
{
    /// <summary>
    /// <c>true</c> when the driver exists and holds a licence valid on <paramref name="on"/>.
    /// </summary>
    /// <param name="on">
    /// The day to test, not "today". A booking that starts in three weeks must be checked against
    /// the licence's state in three weeks, or you cheerfully accept bookings the driver will not
    /// be allowed to drive.
    /// </param>
    Task<bool> CanDriveAsync(Guid driverId, DateOnly on, CancellationToken cancellationToken = default);
}

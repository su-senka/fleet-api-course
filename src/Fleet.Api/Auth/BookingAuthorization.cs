using System.Security.Claims;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Drivers.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace Fleet.Api.Auth;

/// <summary>"May the caller see or change this particular booking?"</summary>
internal sealed class BookingAccessRequirement : IAuthorizationRequirement
{
    public static readonly BookingAccessRequirement Instance = new();
}

/// <summary>
/// Resource-based authorization: a driver may only touch their own bookings.
/// </summary>
/// <remarks>
/// <para>
/// This is the rule that cannot be expressed as a policy on the endpoint, because it depends on
/// the booking. <c>[Authorize(Roles = "fleet.driver")]</c> can say "a driver may call this"; only
/// something holding the booking can say "but not that one".
/// </para>
/// <para>
/// The bridge from a token to a row is <see cref="IDriverDirectory.FindByUserIdAsync"/>. The
/// handler has a username from Keycloak and needs a driver id; asking the Drivers module is the
/// only honest way to get one, and it keeps the mapping in the module that owns it rather than
/// hard-coded here.
/// </para>
/// <para>
/// Note that this only guards a single booking. Keeping a driver's <em>list</em> to their own rows
/// is a different problem, solved differently - see <c>BookingEndpoints</c>, which pins the
/// <c>driverId</c> filter rather than fetching everything and discarding most of it.
/// </para>
/// </remarks>
internal sealed class BookingAccessHandler(IDriverDirectory driverDirectory)
    : AuthorizationHandler<BookingAccessRequirement, BookingDto>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BookingAccessRequirement requirement,
        BookingDto resource)
    {
        // Administrators and dispatchers manage the whole fleet's calendar, so the question does
        // not arise for them.
        if (context.User.IsInRole(FleetRoles.Admin) || context.User.IsInRole(FleetRoles.Dispatcher))
        {
            context.Succeed(requirement);
            return;
        }

        if (!context.User.IsInRole(FleetRoles.Driver))
        {
            return;
        }

        var userName = context.User.UserName();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        var driver = await driverDirectory.FindByUserIdAsync(userName);

        // A signed-in user with the driver role but no matching row is not a driver of anything.
        // Failing closed is the only safe reading.
        if (driver is not null && driver.Id == resource.DriverId)
        {
            context.Succeed(requirement);
        }
    }
}

internal static class BookingAuthorizationExtensions
{
    /// <summary>
    /// The caller's own driver id, or <c>null</c> when they are not a driver.
    /// </summary>
    /// <remarks>
    /// Used by the list endpoint to pin the filter to the caller. Returns <c>null</c> for admins
    /// and dispatchers, who are not restricted to anybody's bookings.
    /// </remarks>
    public static async Task<Guid?> OwnDriverIdAsync(
        this ClaimsPrincipal principal,
        IDriverDirectory driverDirectory,
        CancellationToken cancellationToken = default)
    {
        if (principal.IsInRole(FleetRoles.Admin) || principal.IsInRole(FleetRoles.Dispatcher))
        {
            return null;
        }

        var userName = principal.UserName();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var driver = await driverDirectory.FindByUserIdAsync(userName, cancellationToken);

        return driver?.Id;
    }
}

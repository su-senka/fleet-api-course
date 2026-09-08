namespace Fleet.Api.Auth;

/// <summary>The realm roles from <c>infra/keycloak/realm-export.json</c>, and the policies over them.</summary>
internal static class FleetRoles
{
    public const string Admin = "fleet.admin";
    public const string Dispatcher = "fleet.dispatcher";
    public const string Driver = "fleet.driver";
}

/// <summary>
/// Named authorization policies.
/// </summary>
/// <remarks>
/// Policies rather than <c>[Authorize(Roles = "...")]</c> strings scattered through the endpoints.
/// When "who may cancel a booking" changes, it changes in one place, and the endpoints go on
/// saying what they mean rather than how it is currently decided.
/// </remarks>
internal static class FleetPolicies
{
    /// <summary>Any authenticated member of the fleet.</summary>
    public const string AnyFleetUser = "fleet.any";

    /// <summary>Administrators only: changing the fleet itself.</summary>
    public const string ManageFleet = "fleet.manage";

    /// <summary>Administrators and dispatchers: booking on anybody's behalf.</summary>
    public const string ManageBookings = "fleet.bookings.manage";
}

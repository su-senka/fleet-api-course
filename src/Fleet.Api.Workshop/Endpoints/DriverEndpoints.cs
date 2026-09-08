namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Drivers: the first resource you create and modify rather than only read.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IDriverService</c> in <c>Fleet.Modules.Drivers.Contracts</c>.
/// </para>
/// <para>
/// The interesting part is what the service refuses. Registering a driver whose employee number
/// is already taken comes back as <c>ErrorKind.Conflict</c>; a driver with no name comes back as
/// <c>ErrorKind.Validation</c>. Those are different failures and deserve different status codes,
/// and neither of them is a 500.
/// </para>
/// <para>
/// The seed contains four drivers with no licence and five whose licence has expired. The list
/// takes a <c>hasValidLicence</c> filter, which makes them easy to find - and makes a good
/// argument for why "give me the drivers who cannot drive" is a filter rather than an endpoint.
/// </para>
/// </remarks>
internal static class DriverEndpoints
{
    // TODO(week-4): map GET /drivers, GET /drivers/{driverId} and POST /drivers.
    // TODO(week-5): reject a malformed request before it reaches the module, and return the
    //               failures in one RFC 9457 document rather than one at a time.
    //
    // See docs/assignments/week-4.md and week-5.md.
    // Fleet.Api has no Drivers endpoints - this one is yours from scratch. The shape to follow is
    // src/Fleet.Api/Endpoints/VehicleEndpoints.cs.
}

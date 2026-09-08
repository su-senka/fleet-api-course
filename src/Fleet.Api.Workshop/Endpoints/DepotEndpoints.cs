namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Depots: four rows that never change. The gentlest possible first endpoint.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IDepotService</c> in <c>Fleet.Modules.Vehicles.Contracts</c>. It has
/// two methods, <c>ListAsync</c> and <c>GetAsync</c>, and both return
/// <c>Result&lt;T&gt;</c> rather than a status code - turning that into a response is the work.
/// </para>
/// <para>
/// Two questions to have an answer for before you write a line: what does <c>GET /depots</c>
/// return when there are no depots, and what does <c>GET /depots/{id}</c> return when the id is
/// well-formed but unknown? Neither answer is "throw".
/// </para>
/// </remarks>
internal static class DepotEndpoints
{
    // TODO(week-1): map GET /depots and GET /depots/{depotId}.
    //
    // See docs/assignments/week-1.md.
    // The finished version of this exact resource is in src/Fleet.Api/Endpoints/VehicleEndpoints.cs
    // (MapDepotEndpoints, at the bottom). Try it yourself first; it is twenty lines, and comparing
    // afterwards is worth more than copying now.
    //
    // A skeleton to start from:
    //
    //   public static IEndpointRouteBuilder MapDepotEndpoints(this IEndpointRouteBuilder routes)
    //   {
    //       var group = routes.MapGroup("/depots").WithTags("Depots");
    //
    //       group.MapGet("/", async (IDepotService depots, HttpContext http) => ...);
    //       group.MapGet("/{depotId:guid}", async (Guid depotId, IDepotService depots, HttpContext http) => ...);
    //
    //       return routes;
    //   }
    //
    // Then add app.MapDepotEndpoints() to Program.cs.
}

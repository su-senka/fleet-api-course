namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Work orders: a state machine, and the one call that leaves the process.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IMaintenanceService</c> in
/// <c>Fleet.Modules.Maintenance.Contracts</c>. A work order moves Open to AwaitingParts to
/// InProgress to Completed, or gets cancelled, and each transition is a separate service method.
/// </para>
/// <para>
/// How to expose a state machine over HTTP is a genuine design argument with no settled answer.
/// <c>POST /work-orders/{id}/completion</c>, <c>PATCH</c> with a status field, <c>PUT</c> to a
/// status sub-resource - all three appear in real APIs. Pick one, apply it consistently, and be
/// able to say why.
/// </para>
/// <para>
/// <c>OrderPartsAsync</c> is different from everything else in this repository: it calls an
/// external supplier that returns 500 a quarter of the time, stalls for eight seconds a sixth of
/// the time, and rate-limits after twenty requests a minute. It comes back as
/// <c>ErrorKind.Unavailable</c>. That is the only place that error kind appears, and 503 with a
/// <c>Retry-After</c> is not the same message as 409.
/// </para>
/// </remarks>
internal static class WorkOrderEndpoints
{
    // TODO(week-11): map the work order collection, one work order, and its part lines.
    // TODO(week-12): map ordering parts, and make the typed client survive a supplier that is
    //                having a bad day. The client is registered with no timeout, no retry and no
    //                circuit breaker on purpose - see the TODO(week-12) in
    //                src/Modules/Maintenance/Fleet.Modules.Maintenance/MaintenanceModuleExtensions.cs.
    //
    // See docs/assignments/week-11.md and week-12.md.
    //
    // Measure before you fix. With no timeout, a supplier that stalls for eight seconds holds a
    // request thread for eight seconds, and HttpClient's own default is a hundred. Watch what a
    // dozen concurrent part orders do to an API that is otherwise perfectly healthy.
}

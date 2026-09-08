namespace Fleet.Modules.Maintenance.Contracts;

/// <summary>
/// Where a work order is in the workshop's process.
/// </summary>
/// <remarks>
/// A richer state machine than a booking's, and deliberately so: it is the resource to practise
/// modelling transitions on. Is <c>POST /work-orders/{id}/completion</c> right, or
/// <c>PATCH</c> with a status field, or a <c>PUT</c> to a sub-resource? All three are defensible
/// and the arguments for each are worth having.
/// </remarks>
public enum WorkOrderStatus
{
    /// <summary>Raised, nothing done yet.</summary>
    Open = 1,

    /// <summary>Parts have been ordered from the supplier and have not arrived.</summary>
    AwaitingParts = 2,

    /// <summary>Someone is working on it.</summary>
    InProgress = 3,

    /// <summary>Finished.</summary>
    Completed = 4,

    /// <summary>Abandoned. Kept for the history.</summary>
    Cancelled = 5,
}

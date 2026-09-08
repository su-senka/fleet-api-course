namespace Fleet.Modules.Reporting.Contracts;

/// <summary>
/// Where a report job is in its life.
/// </summary>
/// <remarks>
/// The reason this enum exists at all is that report generation takes about twenty seconds, which
/// is far too long to hold an HTTP request open. The client gets an id immediately and asks again
/// later - which is what <c>202 Accepted</c>, a <c>Location</c> header and a polling endpoint are
/// for, and is the whole point of this module.
/// </remarks>
public enum ReportJobStatus
{
    /// <summary>Accepted, not started. This is what the client sees straight after asking.</summary>
    Queued = 1,

    /// <summary>The background service is working on it.</summary>
    Running = 2,

    /// <summary>Done. <c>ResultBlobId</c> points at the CSV.</summary>
    Completed = 3,

    /// <summary>It went wrong. <c>FailureReason</c> says how.</summary>
    Failed = 4,
}

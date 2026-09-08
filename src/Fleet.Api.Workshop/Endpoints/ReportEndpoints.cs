namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Reports: work that takes longer than a request should.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IReportService</c> in <c>Fleet.Modules.Reporting.Contracts</c>.
/// <c>EnqueueAsync</c> returns in milliseconds with a job id; the CSV appears about twenty
/// seconds later, written to the blob store by a background service.
/// </para>
/// <para>
/// This module exists to make one problem concrete: what do you return when the answer is not
/// ready? <c>202 Accepted</c> with a <c>Location</c> header pointing at the job, a status
/// endpoint the client polls, and something that serves the file once there is one. Getting the
/// codes right along that path is the assignment.
/// </para>
/// <para>
/// <c>OpenResultAsync</c> distinguishes two failures deliberately: a job that does not exist is
/// <c>NotFound</c>, and a job that exists but has produced nothing yet is <c>Conflict</c>. Do not
/// collapse them. A client that cannot tell "wrong id" from "not yet" has no way to know whether
/// to keep polling.
/// </para>
/// </remarks>
internal static class ReportEndpoints
{
    // TODO(week-13): map POST /reports, GET /reports/{jobId}, and the download.
    //
    // See docs/assignments/week-13.md.
    //
    // Things to decide, none of which the service decides for you:
    //   - 202 or 201? The job resource does exist after the POST; the report does not.
    //   - Where does Location point - at the job, or at the file that does not exist yet?
    //   - Should the status response tell a client how long to wait before asking again?
    //   - When the CSV is ready, is that a redirect to the file or the bytes themselves?
}

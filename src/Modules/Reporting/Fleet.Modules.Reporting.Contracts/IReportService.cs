using Fleet.Common.Paging;
using Fleet.Common.Results;

namespace Fleet.Modules.Reporting.Contracts;

/// <summary>
/// Queues reports and reports on their progress.
/// </summary>
/// <remarks>
/// <para>
/// This module exists to make one HTTP problem concrete: what do you return when the work takes
/// longer than a request should? <see cref="EnqueueAsync"/> comes back in milliseconds with a job
/// id, and the CSV appears about twenty seconds later.
/// </para>
/// <para>
/// The endpoints you write over it are the answer: <c>202 Accepted</c> with a <c>Location</c>
/// header pointing at the job, a status endpoint the client polls, and - once it is finished -
/// something that serves the file. Getting the status codes right along that path is the
/// assignment; the service below simply refuses to make any of those decisions for you.
/// </para>
/// </remarks>
public interface IReportService
{
    /// <summary>
    /// Accepts a request for a report and returns immediately.
    /// </summary>
    /// <remarks>
    /// Returns the job in <see cref="ReportJobStatus.Queued"/>. Nothing has been computed yet, and
    /// nothing will be until the background service picks it up.
    /// </remarks>
    Task<Result<ReportJobDto>> EnqueueAsync(
        RequestUtilisationReportCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>The job as it stands. This is what a polling client calls.</summary>
    Task<Result<ReportJobDto>> GetAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of jobs, newest first.
    /// </summary>
    /// <param name="filter">Understands <c>status</c> and <c>kind</c>.</param>
    Task<Result<PagedResult<ReportJobDto>>> ListAsync(
        PageRequest page,
        FilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the finished CSV for reading, or fails when the job is not finished.
    /// </summary>
    /// <returns>
    /// <see cref="ErrorKind.NotFound"/> when there is no such job or its file has gone, and
    /// <see cref="ErrorKind.Conflict"/> when the job exists but has not produced a file yet -
    /// which an endpoint might answer with a 409, or by redirecting to the status resource.
    /// The caller owns the returned stream and must dispose it.
    /// </returns>
    Task<Result<Common.Storage.BlobContent>> OpenResultAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}

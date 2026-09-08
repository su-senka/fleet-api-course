using System.Text.Json;
using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Common.Storage;
using Fleet.Common.Time;
using Fleet.Modules.Reporting.Contracts;
using Fleet.Modules.Reporting.Domain;
using Fleet.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Reporting.Application;

internal sealed class ReportService(
    ReportingDbContext dbContext,
    IBlobStore blobStore,
    IClock clock) : IReportService
{
    public async Task<Result<ReportJobDto>> EnqueueAsync(
        RequestUtilisationReportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validated = Validate(command);
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        var job = ReportJob.Queue(
            Guid.CreateVersion7(),
            ReportKind.MonthlyFleetUtilisation,
            JsonSerializer.Serialize(command, ReportServiceJson.Options),
            clock.UtcNow);

        dbContext.ReportJobs.Add(job);

        // Returns as soon as the row is committed. Nothing has been computed, which is exactly
        // what makes this a 202 rather than a 201 - the resource the client asked for does not
        // exist yet, only the promise of it.
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(job);
    }

    public async Task<Result<ReportJobDto>> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ReportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);

        return job is null ? NotFound(jobId) : ToDto(job);
    }

    public async Task<Result<PagedResult<ReportJobDto>>> ListAsync(
        PageRequest page,
        FilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        var query = dbContext.ReportJobs.AsNoTracking();

        if (filter.EnumTerm<ReportJobStatus>("status") is { } status)
        {
            query = query.Where(job => job.Status == status);
        }

        if (filter.EnumTerm<ReportKind>("kind") is { } kind)
        {
            query = query.Where(job => job.Kind == kind);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var jobs = await query
            .OrderByDescending(job => job.CreatedAt)
            .ThenBy(job => job.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ReportJobDto> items = [.. jobs.Select(ToDto)];

        return new PagedResult<ReportJobDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<BlobContent>> OpenResultAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ReportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);

        if (job is null)
        {
            return NotFound(jobId);
        }

        if (!job.HasResult)
        {
            // Not a 404: the job is real, it simply has nothing to give yet. Telling the two apart
            // is what lets a client know whether to keep polling or give up.
            return Error.Conflict(
                "report_job.not_ready",
                $"This report is {job.Status.ToString().ToLowerInvariant()} and has produced no file.");
        }

        var content = await blobStore.GetAsync(job.ResultBlobId!, cancellationToken);

        return content is null
            ? Error.NotFound(
                "report_job.result_missing",
                "The report was generated but its file is no longer in the blob store.")
            : content;
    }

    private static Result Validate(RequestUtilisationReportCommand command)
    {
        if (command.FromMonth is < 1 or > 12 || command.ToMonth is < 1 or > 12)
        {
            return Error.Validation("report.month_out_of_range", "A month must be between 1 and 12.");
        }

        if (command.FromYear is < 2000 or > 2100 || command.ToYear is < 2000 or > 2100)
        {
            return Error.Validation("report.year_out_of_range", "A year must be between 2000 and 2100.");
        }

        var from = new DateTime(command.FromYear, command.FromMonth, 1);
        var to = new DateTime(command.ToYear, command.ToMonth, 1);

        if (to < from)
        {
            return Error.Validation("report.range_reversed", "The report cannot end before it starts.");
        }

        // A cap, so that a mistyped year does not queue a job that reads a century of bookings.
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;

        if (months > 36)
        {
            return Error.Validation("report.range_too_long", "A report covers at most 36 months.");
        }

        return Result.Success();
    }

    internal static ReportJobDto ToDto(ReportJob job) =>
        new(job.Id,
            job.Kind,
            job.ParametersJson,
            job.Status,
            job.ResultBlobId,
            job.FailureReason,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt);

    private static Error NotFound(Guid jobId) =>
        Error.NotFound("report_job.not_found", $"There is no report job with id {jobId}.");
}

using Fleet.Common.Results;
using Fleet.Modules.Reporting.Contracts;

namespace Fleet.Modules.Reporting.Domain;

/// <summary>A request for a report, and its progress.</summary>
internal sealed class ReportJob
{
    private ReportJob() => ParametersJson = string.Empty;

    private ReportJob(Guid id, ReportKind kind, string parametersJson, DateTimeOffset createdAt)
    {
        Id = id;
        Kind = kind;
        ParametersJson = parametersJson;
        Status = ReportJobStatus.Queued;
        CreatedAt = createdAt;
    }

    public const int BlobIdMaxLength = 200;
    public const int FailureReasonMaxLength = 500;

    public Guid Id { get; private set; }

    public ReportKind Kind { get; private set; }

    /// <summary>
    /// The parameters, stored as JSON.
    /// </summary>
    /// <remarks>
    /// A deliberate piece of untypedness. Every report kind takes different parameters, and the
    /// alternative - a column per parameter of every report anyone ever adds - is worse. The
    /// generator for each kind knows how to read its own.
    /// </remarks>
    public string ParametersJson { get; private set; }

    public ReportJobStatus Status { get; private set; }

    public string? ResultBlobId { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static ReportJob Queue(Guid id, ReportKind kind, string parametersJson, DateTimeOffset createdAt) =>
        new(id, kind, parametersJson, createdAt);

    public Result Start(DateTimeOffset startedAt)
    {
        if (Status != ReportJobStatus.Queued)
        {
            return Error.Conflict(
                "report_job.not_queued",
                $"A {Status.ToString().ToLowerInvariant()} job cannot be started.");
        }

        Status = ReportJobStatus.Running;
        StartedAt = startedAt;

        return Result.Success();
    }

    public void Complete(string resultBlobId, DateTimeOffset completedAt)
    {
        Status = ReportJobStatus.Completed;
        ResultBlobId = resultBlobId;
        CompletedAt = completedAt;
        FailureReason = null;
    }

    public void Fail(string reason, DateTimeOffset failedAt)
    {
        Status = ReportJobStatus.Failed;
        FailureReason = reason.Length > FailureReasonMaxLength ? reason[..FailureReasonMaxLength] : reason;
        CompletedAt = failedAt;
    }

    /// <summary>Whether there is a file to serve. False for everything except a completed job.</summary>
    public bool HasResult => Status == ReportJobStatus.Completed && ResultBlobId is not null;
}

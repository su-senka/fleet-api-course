namespace Fleet.Modules.Reporting.Contracts;

/// <summary>
/// A report job.
/// </summary>
/// <param name="ParametersJson">
/// The parameters the job was asked for, as stored. Opaque to everything except the generator that
/// understands the <see cref="ReportKind"/>.
/// </param>
/// <param name="ResultBlobId">
/// Where the finished CSV lives in the blob store, once <see cref="ReportJobStatus.Completed"/>.
/// A blob id, not a URL - turning it into something a browser can fetch is the API layer's
/// decision, and a more interesting one than it first looks.
/// </param>
public sealed record ReportJobDto(
    Guid Id,
    ReportKind Kind,
    string ParametersJson,
    ReportJobStatus Status,
    string? ResultBlobId,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Asks for a monthly fleet utilisation report.
/// </summary>
/// <remarks>
/// The window is inclusive of both months: January to March is three months of rows.
/// </remarks>
public sealed record RequestUtilisationReportCommand(int FromYear, int FromMonth, int ToYear, int ToMonth);

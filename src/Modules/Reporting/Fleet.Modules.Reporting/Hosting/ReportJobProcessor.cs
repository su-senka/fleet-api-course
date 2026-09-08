using System.Text.Json;
using Fleet.Common.Storage;
using Fleet.Common.Time;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Reporting.Application;
using Fleet.Modules.Reporting.Contracts;
using Fleet.Modules.Reporting.Domain;
using Fleet.Modules.Reporting.Persistence;
using Fleet.Modules.Vehicles.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Modules.Reporting.Hosting;

/// <summary>How the report generator behaves.</summary>
public sealed class ReportingOptions
{
    public const string SectionName = "Reporting";

    /// <summary>How often to look for queued jobs.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a report is made to take, in total.
    /// </summary>
    /// <remarks>
    /// Yes, artificial. Computing utilisation over 250 vehicles takes a few milliseconds, which
    /// would make the asynchronous job pattern look like pointless ceremony. Twenty seconds is
    /// long enough that holding an HTTP request open is obviously the wrong answer, which is the
    /// lesson. Set it to zero in tests.
    /// </remarks>
    public TimeSpan SimulatedDuration { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>The blob container the CSVs go into.</summary>
    public string Container { get; set; } = "reports";

    /// <summary>Set false to stop the processor. The tests do this.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Turns queued report jobs into CSV files in the blob store.
/// </summary>
/// <remarks>
/// One job at a time, oldest first. A real system would run several and would need to stop two
/// instances picking up the same row - <c>FOR UPDATE SKIP LOCKED</c>, most likely. Here there is
/// one process and one worker, and saying so is more honest than pretending otherwise.
/// </remarks>
internal sealed class ReportJobProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportingOptions> options,
    ILogger<ReportJobProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Report processing is disabled");
            return;
        }

        logger.LogInformation(
            "Report job processor started, polling every {PollInterval}, taking {Duration} per report",
            settings.PollInterval,
            settings.SimulatedDuration);

        using var timer = new PeriodicTimer(settings.PollInterval);

        do
        {
            await ProcessNextAsync(settings, stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task ProcessNextAsync(ReportingOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var job = await dbContext.ReportJobs
                .Where(candidate => candidate.Status == ReportJobStatus.Queued)
                .OrderBy(candidate => candidate.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (job is null)
            {
                return;
            }

            // Claim it before doing anything slow, so a polling client sees Running rather than
            // twenty seconds of Queued and concludes nothing is happening.
            var started = job.Start(clock.UtcNow);
            if (started.IsFailure)
            {
                return;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Generating report {JobId} ({Kind})", job.Id, job.Kind);

            try
            {
                var blobId = await GenerateAsync(scope.ServiceProvider, job, settings, cancellationToken);

                job.Complete(blobId, clock.UtcNow);

                logger.LogInformation("Report {JobId} finished as {BlobId}", job.Id, blobId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Recorded on the row rather than thrown. A client polling the job needs to be
                // told it failed and why; a background service that crashed tells it nothing.
                logger.LogError(exception, "Report {JobId} failed", job.Id);

                job.Fail(exception.Message, clock.UtcNow);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Report processing tick failed. Will try again");
        }
    }

    private static async Task<string> GenerateAsync(
        IServiceProvider services,
        ReportJob job,
        ReportingOptions settings,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<RequestUtilisationReportCommand>(
            job.ParametersJson, ReportServiceJson.Options)
            ?? throw new InvalidOperationException($"Report job {job.Id} has unreadable parameters.");

        var from = new DateTimeOffset(parameters.FromYear, parameters.FromMonth, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(parameters.ToYear, parameters.ToMonth, 1, 0, 0, 0, TimeSpan.Zero)
            .AddMonths(1);

        var vehicles = await services.GetRequiredService<IVehicleCatalog>()
            .GetAllAsync(cancellationToken);

        var bookings = await services.GetRequiredService<IBookingCalendar>()
            .GetBookedPeriodsAsync(from, to, cancellationToken);

        var rows = UtilisationReportGenerator.Build(vehicles, bookings, from, to.AddMonths(-1));
        var csv = UtilisationReportGenerator.ToCsv(rows);

        // The artificial part. Everything above takes milliseconds.
        if (settings.SimulatedDuration > TimeSpan.Zero)
        {
            await Task.Delay(settings.SimulatedDuration, cancellationToken);
        }

        using var content = new MemoryStream(csv);

        return await services.GetRequiredService<IBlobStore>().SaveAsync(
            settings.Container,
            $"utilisation-{parameters.FromYear}-{parameters.FromMonth:00}-to-{parameters.ToYear}-{parameters.ToMonth:00}.csv",
            content,
            "text/csv",
            cancellationToken);
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

using Fleet.Common.Messaging.Outbox;
using Fleet.Common.Time;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Domain;
using Fleet.Modules.Drivers.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Modules.Drivers.Hosting;

/// <summary>How often to look for expiring certificates, and how far ahead to look.</summary>
public sealed class CertificateExpiryOptions
{
    public const string SectionName = "Drivers:CertificateExpiry";

    /// <summary>The window the brief specifies: warn 30 days out.</summary>
    public int WarnWithinDays { get; set; } = 30;

    /// <summary>
    /// How often to sweep. Hourly is far more often than needed for a rule measured in days, and
    /// cheap enough not to matter - but it means a demonstration does not take a day to start.
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Set false to stop the scanner. The tests do this.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Announces certificates that are about to expire.
/// </summary>
/// <remarks>
/// <para>
/// The publishing half of the outbox pattern, and the only place in the repository that raises an
/// integration event. Note what it does <em>not</em> do: it does not send an email, or write to the
/// notifications table, or know that Notifications exists. It states a fact - this certificate
/// expires in nine days - and stops.
/// </para>
/// <para>
/// The two writes, marking the certificate as warned and enqueuing the event, go in one
/// <c>SaveChangesAsync</c>. If the process dies immediately afterwards, the outbox row is there
/// and the next sweep of <c>OutboxProcessor</c> publishes it. If it dies before, neither happened
/// and the next scan finds the certificate again.
/// </para>
/// </remarks>
internal sealed class CertificateExpiryScanner(
    IServiceScopeFactory scopeFactory,
    IOptions<CertificateExpiryOptions> options,
    ILogger<CertificateExpiryScanner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Certificate expiry scanning is disabled");
            return;
        }

        logger.LogInformation(
            "Certificate expiry scanner started, warning {Days} days ahead every {Interval}",
            settings.WarnWithinDays,
            settings.ScanInterval);

        using var timer = new PeriodicTimer(settings.ScanInterval);

        do
        {
            await ScanAsync(settings.WarnWithinDays, stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task ScanAsync(int warnWithinDays, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<DriversDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var today = clock.Today;
            var horizon = today.AddDays(warnWithinDays);

            // Certificates already warned about are excluded in the query, not filtered afterwards.
            // Once every driver has been warned, this sweep costs one indexed query returning
            // nothing, which is what it should cost.
            var expiring = await dbContext.Certificates
                .Where(certificate =>
                    certificate.SupersededAt == null
                    && certificate.ExpiryWarningSentAt == null
                    && certificate.ExpiresOn >= today
                    && certificate.ExpiresOn <= horizon)
                .Join(
                    dbContext.Drivers,
                    certificate => certificate.DriverId,
                    driver => driver.Id,
                    (certificate, driver) => new { certificate, driver })
                .ToListAsync(cancellationToken);

            if (expiring.Count == 0)
            {
                logger.LogDebug("No certificates expiring within {Days} days", warnWithinDays);
                return;
            }

            var announced = 0;

            foreach (var row in expiring)
            {
                if (!row.certificate.TryMarkExpiryWarningSent(clock.UtcNow))
                {
                    continue;
                }

                outbox.Enqueue(new CertificateExpiringSoon(
                    row.certificate.Id,
                    row.driver.Id,
                    row.driver.Name,
                    row.driver.EmployeeNumber,
                    row.certificate.Kind,
                    row.certificate.ExpiresOn,
                    row.certificate.ExpiresOn.DayNumber - today.DayNumber));

                announced++;
            }

            // One transaction: the warnings are recorded and the events are queued, or neither.
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Queued {Count} certificate expiry warning(s) within {Days} days",
                announced,
                warnWithinDays);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Certificate expiry scan failed. Will try again on the next tick");
        }
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

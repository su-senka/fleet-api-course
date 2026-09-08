using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Common.Messaging.Outbox;

/// <summary>How often to sweep the outbox tables, and how much to take each time.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// The gap between sweeps. Short enough that an event feels immediate, long enough that an
    /// idle system is not running two queries a second forever.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Messages taken per module per sweep.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Set false to stop the sweeper entirely. The tests do this.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// The background service that drains every module's outbox.
/// </summary>
/// <remarks>
/// <para>
/// It resolves every registered <see cref="IOutboxDispatcher"/> and gives each a turn, so it never
/// learns how many modules there are. Adding a seventh publishing module means registering one more
/// dispatcher and changing nothing here.
/// </para>
/// <para>
/// One instance per process, and this repository runs one process. Two instances would happily
/// dispatch the same message twice, because nothing here takes a lock on the row - a real
/// deployment would need <c>FOR UPDATE SKIP LOCKED</c> or a leader election, and neither is worth
/// the complexity in a teaching repository that runs on a laptop.
/// </para>
/// </remarks>
internal sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Outbox processing is disabled");
            return;
        }

        logger.LogInformation(
            "Outbox processor started, sweeping every {PollInterval}", settings.PollInterval);

        using var timer = new PeriodicTimer(settings.PollInterval);

        // Sweep once immediately, then on the timer. Waiting five seconds before the first sweep
        // makes every fresh start look broken for five seconds.
        do
        {
            await SweepAsync(settings.BatchSize, stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));

        logger.LogInformation("Outbox processor stopping");
    }

    private async Task SweepAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            foreach (var dispatcher in scope.ServiceProvider.GetServices<IOutboxDispatcher>())
            {
                var published = await dispatcher.DispatchPendingAsync(batchSize, cancellationToken);

                if (published > 0)
                {
                    logger.LogInformation(
                        "Published {Count} outbox message(s) from {Module}",
                        published,
                        dispatcher.ModuleName);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A background service that throws stops for good, taking every module's outbox with
            // it. A database that is briefly unreachable must not be able to do that.
            logger.LogError(exception, "Outbox sweep failed. Will try again on the next tick");
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

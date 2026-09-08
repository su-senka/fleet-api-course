using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fleet.Api.Hosting;

/// <summary>
/// Liveness and readiness probes.
/// </summary>
/// <remarks>
/// The distinction matters and is routinely got wrong. <c>/health/live</c> answers "is this
/// process worth keeping alive?" and must not touch a dependency - a database outage should not
/// make an orchestrator restart a perfectly healthy API. <c>/health/ready</c> answers "can this
/// instance serve traffic right now?" and does check dependencies, so a load balancer can take
/// the instance out of rotation without killing it.
/// </remarks>
internal static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    public static IHostApplicationBuilder AddFleetHealthChecks(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Fleet");
        var blobConnectionString = builder.Configuration.GetConnectionString("Blobs");
        var supplierUrl = builder.Configuration["Supplier:BaseUrl"];

        var healthChecks = builder.Services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            healthChecks.AddNpgSql(
                connectionString,
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag]);
        }

        if (!string.IsNullOrWhiteSpace(blobConnectionString))
        {
            healthChecks.AddAzureBlobStorage(
                _ => new Azure.Storage.Blobs.BlobServiceClient(blobConnectionString),
                name: "blobs",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag]);
        }

        if (!string.IsNullOrWhiteSpace(supplierUrl))
        {
            // The supplier is deliberately unreliable, so a bad answer from it must not make the
            // whole API unready - hence Degraded rather than Unhealthy.
            healthChecks.AddUrlGroup(
                new Uri(new Uri(supplierUrl), "health"),
                name: "supplier",
                failureStatus: HealthStatus.Degraded,
                tags: [ReadyTag]);
        }

        return builder;
    }

    public static WebApplication MapFleetHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // No predicate matches no checks, which is exactly what liveness should run.
            Predicate = static _ => false,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static check => check.Tags.Contains(ReadyTag),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        return app;
    }
}

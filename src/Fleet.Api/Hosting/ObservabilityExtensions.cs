using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace Fleet.Api.Hosting;

/// <summary>
/// Structured logs to Seq and distributed traces to Jaeger.
/// </summary>
/// <remarks>
/// <para>
/// Both are running in <c>docker compose</c>, so there is nothing to sign up for. Logs land at
/// <c>http://localhost:8081</c> and traces at <c>http://localhost:16686</c>.
/// </para>
/// <para>
/// The near-identical file in <c>Fleet.Api.Workshop</c> is duplicated rather than shared. The two
/// hosts are read side by side, and a student should be able to understand either one without
/// opening a third project.
/// </para>
/// </remarks>
internal static class ObservabilityExtensions
{
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The logger is disposed by the host on shutdown via UseSerilog.")]
    public static IHostApplicationBuilder AddFleetObservability(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        var seqUrl = builder.Configuration["Observability:SeqUrl"];
        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"];

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            loggerConfiguration.WriteTo.Seq(seqUrl);
        }

        builder.Services.AddSerilog(loggerConfiguration.CreateLogger(), dispose: true);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                .AddHttpClientInstrumentation()
                .AddOtlpExporterIfConfigured(otlpEndpoint))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporterIfConfigured(otlpEndpoint));

        return builder;
    }

    private static TracerProviderBuilder AddOtlpExporterIfConfigured(
        this TracerProviderBuilder builder,
        string? endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
        }

        return builder;
    }

    private static MeterProviderBuilder AddOtlpExporterIfConfigured(
        this MeterProviderBuilder builder,
        string? endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
        }

        return builder;
    }

    /// <summary>
    /// One structured log line per request instead of the framework's four, with the route
    /// template intact so Seq can group by endpoint rather than by URL.
    /// </summary>
    public static IApplicationBuilder UseFleetRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RouteTemplate", httpContext.GetEndpoint()?.DisplayName);
            };
        });

        return app;
    }
}

using Serilog;
using Serilog.Events;
using Supplier.Api.Fake;

// -------------------------------------------------------------------------------------------
// Supplier.Api.Fake - the parts supplier that keeps letting you down.
//
// It is a real HTTP service with real endpoints, and it fails on purpose: a quarter of requests
// return 500, a sixth take eight seconds, more than twenty a minute get 429, and if you ask for
// it, every so often it goes away entirely for a minute.
//
// None of that is random in the unhelpful sense. Every decision is derived from FAKE_SEED and the
// request's sequence number, so the same run produces the same failures and an experiment can be
// repeated. See FailureInjection.cs, which is worth reading before week 12.
//
// Every decision is logged, with the sequence number, so you can line your circuit breaker's
// state changes up against what actually happened upstream.
// -------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

var seqUrl = builder.Configuration["SEQ_URL"];

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "supplier-fake")
    .WriteTo.Console();

if (!string.IsNullOrWhiteSpace(seqUrl))
{
    // Same Seq instance the APIs log to, so both sides of the conversation are in one place.
    loggerConfiguration.WriteTo.Seq(seqUrl);
}

builder.Services.AddSerilog(loggerConfiguration.CreateLogger(), dispose: true);

var settings = FakeSettings.FromEnvironment(builder.Configuration);

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FailureInjector>();
builder.Services.AddSingleton<OrderStore>();

var app = builder.Build();

app.Logger.LogInformation(
    "Supplier fake starting. seed={Seed} errorRate={ErrorRate} slowRate={SlowRate} "
    + "slowMs={SlowMs} rateLimitPerMinute={RateLimit} outageAfter={OutageAfter}",
    settings.Seed,
    settings.ErrorRate,
    settings.SlowRate,
    settings.SlowMilliseconds,
    settings.RateLimitPerMinute,
    settings.OutageAfter?.ToString() ?? "(never)");

// -------------------------------------------------------------------------------------------
// Health. Deliberately outside the failure injection: docker compose watches this endpoint, and
// a container that reports itself unhealthy a quarter of the time is no use to anybody.
// -------------------------------------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Applies the injected failure, or returns null to carry on and serve the request.
async Task<IResult?> InterfereAsync(
    HttpContext context,
    FailureInjector injector,
    ILogger<Program> logger,
    string endpoint)
{
    var decision = injector.Decide();

    logger.LogInformation(
        "Supplier request #{Sequence} to {Endpoint}: {Verdict}",
        decision.Sequence,
        endpoint,
        decision.Verdict);

    switch (decision.Verdict)
    {
        case FakeVerdict.Outage:
            context.Response.Headers.RetryAfter = SecondsHeader(decision.RetryAfter);

            return Results.Json(
                new { error = "The supplier is temporarily unavailable." },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        case FakeVerdict.RateLimited:
            // Retry-After in seconds, per RFC 9110. A resilience pipeline that reads this header
            // waits exactly long enough; one that guesses either hammers the service or oversleeps.
            context.Response.Headers.RetryAfter = SecondsHeader(decision.RetryAfter);

            return Results.Json(
                new { error = "Too many requests." },
                statusCode: StatusCodes.Status429TooManyRequests);

        case FakeVerdict.ServerError:
            return Results.Json(
                new { error = "The supplier's order system fell over." },
                statusCode: StatusCodes.Status500InternalServerError);

        case FakeVerdict.ServeSlowly:
            // The interesting failure. It is not an error - a perfectly good response arrives
            // eventually - which is exactly why the timeout has to be the caller's decision.
            await Task.Delay(decision.Delay!.Value);
            return null;

        default:
            return null;
    }
}

static string SecondsHeader(TimeSpan? retryAfter) =>
    Math.Max(1, (int)Math.Ceiling(retryAfter?.TotalSeconds ?? 1))
        .ToString(System.Globalization.CultureInfo.InvariantCulture);

// -------------------------------------------------------------------------------------------
// Orders.
// -------------------------------------------------------------------------------------------
app.MapPost("/orders", async Task<IResult> (
    HttpContext context,
    SupplierOrderRequest request,
    FailureInjector injector,
    OrderStore store,
    ILogger<Program> logger) =>
{
    if (await InterfereAsync(context, injector, logger, "POST /orders") is { } interference)
    {
        return interference;
    }

    if (request.Lines.Count == 0)
    {
        return Results.BadRequest(new { error = "An order needs at least one line." });
    }

    var order = store.Add(request, DateTimeOffset.UtcNow);

    logger.LogInformation(
        "Accepted order {OrderId} for work order {WorkOrderId} with {LineCount} line(s)",
        order.OrderId,
        order.WorkOrderId,
        order.Lines.Count);

    return Results.Created($"/orders/{order.OrderId}", order);
});

app.MapGet("/orders/{orderId}", async Task<IResult> (
    HttpContext context,
    string orderId,
    FailureInjector injector,
    OrderStore store,
    ILogger<Program> logger) =>
{
    if (await InterfereAsync(context, injector, logger, "GET /orders/{orderId}") is { } interference)
    {
        return interference;
    }

    var order = store.Find(orderId);

    return order is null
        ? Results.NotFound(new { error = $"No order with id {orderId}." })
        : Results.Ok(order);
});

await app.RunAsync();

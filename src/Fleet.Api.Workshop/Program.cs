using Fleet.Api.Workshop.Hosting;
using Fleet.Common;
using Fleet.Common.Persistence;
using Fleet.Modules.Bookings;
using Fleet.Modules.Drivers;
using Fleet.Modules.Maintenance;
using Fleet.Modules.Notifications;
using Fleet.Modules.Reporting;
using Fleet.Modules.Vehicles;
using Scalar.AspNetCore;

// -------------------------------------------------------------------------------------------
// Fleet.Api.Workshop - your host.
//
// Every module below is finished, tested and seeded. None of it is exposed over HTTP yet, and
// that is the whole course: you write the endpoints.
//
// Run it now and open http://localhost:5101/scalar. The API documentation is empty. Your job for
// the next fourteen weeks is to fill it in, one week at a time, using Fleet.Api on :5100 as the
// worked example for the two resources that are already done.
//
// Start at docs/assignments/week-1.md, then look in Endpoints/ for the stub that matches.
// -------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

builder.AddFleetObservability(serviceName: "fleet-api-workshop");
builder.AddFleetHealthChecks();

builder.Services.AddFleetCommon();

// Blob storage, the idempotency key store, and the background service that drains every module's
// outbox. Separate from AddFleetCommon because it needs configuration and real external services.
builder.Services.AddFleetInfrastructure(builder.Configuration);
builder.Services.AddVehiclesModule(builder.Configuration);
builder.Services.AddDriversModule(builder.Configuration);
builder.Services.AddBookingsModule(builder.Configuration);
builder.Services.AddMaintenanceModule(builder.Configuration);
builder.Services.AddReportingModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
}

var app = builder.Build();

// In Development, bring the schemas up to date and seed them on the way in, so that a fresh
// clone is one `docker compose up` and one `dotnet run` away from 250 vehicles to query.
// `dotnet run -- --seed` does the same and exits, which is what `make seed` uses.
var seedOnly = args.Contains("--seed", StringComparer.OrdinalIgnoreCase);

// Set Database:Initialize to false to start the host against a database you do not want it to
// touch - which is exactly what the integration tests do.
var initializeDatabase = seedOnly
    || (app.Environment.IsDevelopment() && app.Configuration.GetValue("Database:Initialize", true));

if (initializeDatabase)
{
    await app.InitializeModulesAsync(seed: true);
}

if (seedOnly)
{
    return;
}

app.UseFleetRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Fleet API (workshop)"));
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapFleetHealthChecks();

// Your endpoint groups get mapped here, one call per resource. See Endpoints/.

await app.RunAsync();

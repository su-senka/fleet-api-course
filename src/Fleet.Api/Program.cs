using Fleet.Api.Hosting;
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
// Fleet.Api - the reference host.
//
// This host implements the Vehicles and Bookings endpoints in full. It is the example students
// read; the endpoints they write go into Fleet.Api.Workshop, which registers exactly the same
// modules and exposes none of them.
//
// Notice what is NOT here: no business logic. Every module below is finished and tested. This
// project's entire job is to put HTTP in front of it.
// -------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

builder.AddFleetObservability(serviceName: "fleet-api");
builder.AddFleetHealthChecks();

// The shared kernel, then one call per module. A host knows the module's registration method
// and nothing else about it.
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
    // Wide open on purpose: the React client is a separate repository and we do not want CORS to
    // be anybody's first lesson. This branch never runs outside Development.
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
    app.MapScalarApiReference(options => options.WithTitle("Fleet API (reference)"));
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapFleetHealthChecks();

// The Vehicles and Bookings endpoint groups are mapped here, one call per resource.

await app.RunAsync();

/// <summary>Exposed so the integration tests can drive this host with WebApplicationFactory.</summary>
public partial class Program;

using Fleet.Api.Auth;
using Fleet.Api.Endpoints;
using Fleet.Api.Hosting;
using Fleet.Api.Http;
using Fleet.Api.Middleware;
using Fleet.Api.OpenApi;
using Fleet.Common;
using Fleet.Common.Persistence;
using Fleet.Modules.Bookings;
using Fleet.Modules.Drivers;
using Fleet.Modules.Maintenance;
using Fleet.Modules.Notifications;
using Fleet.Modules.Reporting;
using Fleet.Modules.Vehicles;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;

// -------------------------------------------------------------------------------------------
// Fleet.Api - the reference host.
//
// This host implements the Vehicles and Bookings endpoints in full. It is the example students
// read; the endpoints they write go into Fleet.Api.Workshop, which registers exactly the same
// modules and exposes none of them.
//
// Notice what is NOT here: no business logic. Every module below is finished and tested. This
// project's entire job is to put HTTP in front of it - routing, status codes, headers, caching,
// authorization and the mapping between wire shapes and domain commands.
// -------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

builder.AddFleetObservability(serviceName: "fleet-api");
builder.AddFleetHealthChecks();
builder.AddFleetAuthentication();

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

// "A driver may only touch their own bookings" - the one rule that needs the resource itself.
builder.Services.AddScoped<IAuthorizationHandler, BookingAccessHandler>();

// includeInternalTypes matters: the validators are internal, like nearly everything else here,
// and the scanner skips non-public types by default. Without it they are silently not registered
// and the validation filter fails at the first request rather than at startup.
builder.Services.AddValidatorsFromAssemblyContaining<Program>(
    ServiceLifetime.Singleton, includeInternalTypes: true);

// Enums as names, not numbers. "status": "InMaintenance" tells a reader what it means;
// "status": 2 makes them go and find the enum. It also means a value read from a response can be
// handed straight back as a query-string filter, which is what a client will try first.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOutputCache();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<FleetDocumentTransformer>();
    options.AddOperationTransformer<FleetExampleTransformer>();
});

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

// Order matters, and this is the order:
//   caching before auth would serve one user's response to another;
//   idempotency after auth, so an unauthenticated request never claims a key.
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();
app.UseFleetIdempotency();

app.MapFleetHealthChecks();

// The Vehicles and Bookings endpoint groups, one call per resource.
app.MapVehicleEndpoints();
app.MapDepotEndpoints();
app.MapBookingEndpoints();

await app.RunAsync();

/// <summary>Exposed so the integration tests can drive this host with WebApplicationFactory.</summary>
public partial class Program;

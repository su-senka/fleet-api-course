using Fleet.Api.Hosting;
using Fleet.Common;
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

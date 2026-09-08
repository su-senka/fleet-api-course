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
// Every module registered below is finished, tested and seeded: 250 vehicles, 60 drivers, 3,002
// bookings, 400 work orders. None of it is reachable over HTTP, and that is the whole course.
//
// Run it and open http://localhost:5101/scalar. The API documentation is empty. Filling it in,
// one week at a time, is the work - with Fleet.Api on :5100 as the finished example of the two
// resources you can compare against.
//
//   docs/assignments/week-1.md   what to do first
//   Endpoints/                   a stub file per resource, each pointing at its week
//   src/Fleet.Api/               the reference. Read it. It is meant to be read.
//
// Nothing below this line is business logic, and nothing you add should be either. If you find
// yourself writing a rule in an endpoint, it belongs in a module - and the module probably
// already has it.
// -------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Structured logs to Seq and traces to Jaeger. Already wired; look at these two files when you
// want to know why a request was slow.
builder.AddFleetObservability(serviceName: "fleet-api-workshop");
builder.AddFleetHealthChecks();

// The shared kernel, then one call per module. A host knows a module's registration method and
// nothing else about it.
builder.Services.AddFleetCommon();

// Blob storage, the idempotency key store, and the background service that drains every module's
// outbox. The store is finished; the middleware that uses it is week 10.
builder.Services.AddFleetInfrastructure(builder.Configuration);

builder.Services.AddVehiclesModule(builder.Configuration);
builder.Services.AddDriversModule(builder.Configuration);
builder.Services.AddBookingsModule(builder.Configuration);
builder.Services.AddMaintenanceModule(builder.Configuration);
builder.Services.AddReportingModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// TODO(week-5): register your FluentValidation validators here.
//   builder.Services.AddValidatorsFromAssemblyContaining<Program>(
//       ServiceLifetime.Singleton, includeInternalTypes: true);
// See docs/assignments/week-5.md. Note includeInternalTypes - without it, internal validators
// are silently not registered and the failure arrives at the first request rather than at startup.

// TODO(week-7): add JWT bearer authentication against the Keycloak realm.
//   builder.AddFleetAuthentication();
// Keycloak is already running in Compose with six users and three roles; requests/auth.http gets
// you a token. See docs/assignments/week-7.md, and read Fleet.Api/Auth/AuthenticationExtensions.cs
// afterwards - there is one thing in there about Keycloak's roles that costs everyone an hour.

// TODO(week-8): add authorization policies, and the handler that keeps a driver to their own
// bookings. See docs/assignments/week-8.md.

// TODO(week-11): add output caching.
//   builder.Services.AddOutputCache();
// See docs/assignments/week-11.md, and read Fleet.Api/Http/VehicleListCachePolicy.cs before you
// cache anything that a signed-in caller can see.

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
    app.MapScalarApiReference(options => options.WithTitle("Fleet API (workshop)"));
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// TODO(week-11): app.UseOutputCache();  - before authentication, and read the policy first.
// TODO(week-7):  app.UseAuthentication();
// TODO(week-8):  app.UseAuthorization();
// TODO(week-10): app.UseFleetIdempotency();
//
// The order of these four is not arbitrary and getting it wrong is a security bug rather than a
// mistake: caching before authentication can serve one user's response to another. Fleet.Api's
// Program.cs has them in a working order, with a note on why.

app.MapFleetHealthChecks();

// -------------------------------------------------------------------------------------------
// Your endpoints get mapped here, one call per resource, as you write them.
//
//   app.MapDepotEndpoints();          TODO(week-1)
//   app.MapVehicleEndpoints();        TODO(week-2)
//   app.MapDriverEndpoints();         TODO(week-4)
//   app.MapCertificateEndpoints();    TODO(week-6)
//   app.MapBookingEndpoints();        TODO(week-7)
//   app.MapWorkOrderEndpoints();      TODO(week-12)
//   app.MapReportEndpoints();         TODO(week-13)
//   app.MapNotificationEndpoints();   TODO(week-14)
//
// Each has a stub in Endpoints/ with the module contract it should call and the week that asks
// for it. Uncommenting a line before writing the endpoint will not compile, which is the point.
// -------------------------------------------------------------------------------------------

await app.RunAsync();

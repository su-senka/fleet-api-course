// -------------------------------------------------------------------------------------------
// Supplier.Api.Fake - the parts supplier that keeps letting you down.
//
// Milestone 1 ships the health endpoint only, so that docker compose has something to wait for.
// The order endpoints and the failure injection (error rate, slow responses, rate limiting,
// scheduled outages, all reproducible from FAKE_SEED) arrive in milestone 4.
// -------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await app.RunAsync();

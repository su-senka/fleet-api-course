// -------------------------------------------------------------------------------------------
// Fleet.Web.Workshop - the BFF host for the React course.
//
// Two halves live here. This project owns the browser session and the routes the SPA calls;
// ClientApp/ is the React application itself. In development you run both:
//
//   dotnet run --project src/Fleet.Web.Workshop      this host, on :5180
//   npm run dev   (in ClientApp/)                    Vite, on :5174, proxying back to :5180
//
// The browser talks only to Vite, Vite forwards /bff and /api here, and this host forwards /api
// on to the Fleet API. In production there is no Vite: this host serves the built assets itself
// and the proxy hop disappears.
// -------------------------------------------------------------------------------------------

using Fleet.Web.Workshop.Extensions;
using Fleet.Web.Workshop.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPresentation(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Serve ClientApp's built output. In development this finds nothing, because Vite is serving the
// application instead - that is expected, and is why the dev loop needs two terminals.
app.UseDefaultFiles();
app.UseStaticFiles();

// Must run before authentication: an unauthenticated request should not get as far as the
// session cookie being read.
app.UseMiddleware<CsrfHeaderMiddleware>();

// Order is the same rule as in the API course: authentication before authorization, and both
// before anything that reads the user.
app.UseAuthentication();
app.UseAuthorization();

// These three are the whole contract between the SPA and the session.
app.MapBffEndpoints();

// Forwards /api to the Fleet API with the caller's access token attached.
app.MapReverseProxy();

// Client-side routing: any path this host does not recognize serves the SPA entry point and
// lets React Router decide what it means. Must be last - it matches everything.
app.MapFallbackToFile("index.html");

await app.RunAsync();

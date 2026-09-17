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
//
//   docs/react-client/week-1.md   the skeleton and the first call
//   docs/react-client/week-2.md   the BFF: OIDC, the session cookie, and the authenticated proxy
// -------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// TODO(week-2): register the cookie session, the OIDC handler, the ApiProxy policy and YARP.
//   builder.Services.AddPresentation(builder.Configuration);
// See Extensions/ServiceCollectionExtensions.cs - every piece is stubbed there with its own
// TODO, and docs/react-client/week-2.md explains why a BFF exists at all.

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Serve ClientApp's built output. In development this finds nothing, because Vite is serving the
// application instead - that is expected, and is why the dev loop needs two terminals.
app.UseDefaultFiles();
app.UseStaticFiles();

// TODO(week-2): reject any /api request that does not carry the X-CSRF header.
//   app.UseMiddleware<CsrfHeaderMiddleware>();
// Must run before authentication: an unauthenticated request should not get as far as the
// session cookie being read. See Middleware/CsrfHeaderMiddleware.cs.

// TODO(week-2): app.UseAuthentication();
// TODO(week-2): app.UseAuthorization();
//
// Order is the same rule as in the API course: authentication before authorization, and both
// before anything that reads the user. Fleet.Api/Program.cs has the equivalent note.

// TODO(week-2): map /bff/login, /bff/logout and /bff/user.
//   app.MapBffEndpoints();
// These three are the whole contract between the SPA and the session. See
// Extensions/EndpointsBuilderExtensions.cs.

// TODO(week-2): forward /api to the Fleet API with the caller's access token attached.
//   app.MapReverseProxy();
// The destination is configured in appsettings.json, not here.

// Client-side routing: any path this host does not recognise serves the SPA entry point and
// lets React Router decide what it means. Must be last - it matches everything.
app.MapFallbackToFile("index.html");

await app.RunAsync();

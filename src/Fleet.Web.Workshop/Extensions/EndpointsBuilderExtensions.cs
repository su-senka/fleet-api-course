namespace Fleet.Web.Workshop.Extensions;

/// <summary>
/// The three routes that are the entire contract between the SPA and its session.
/// </summary>
/// <remarks>
/// A SPA cannot "call" a login the way it calls an API - OIDC needs a full-page redirect to the
/// identity provider and back. So login and logout are navigations, not fetches, and
/// <c>/bff/user</c> is the one endpoint the SPA polls to find out how it went.
/// </remarks>
internal static class EndpointsBuilderExtensions
{
    public static void MapBffEndpoints(this WebApplication app)
    {
        // TODO(week-2): GET /bff/login?returnUrl=... - issue an OIDC challenge, then come back to
        // returnUrl. Allow anonymous, obviously.
        //
        // Validate returnUrl before you use it. An absolute URL here turns your login endpoint
        // into an open redirect: an attacker sends a victim to your own trusted domain and lands
        // them on theirs, post-authentication. Accept only paths beginning with a single "/" -
        // and note that "//evil.example" also begins with "/".

        // TODO(week-2): GET /bff/logout - sign out of both the cookie and OIDC, so the user is
        // signed out of Keycloak too rather than being silently signed straight back in.
        // Requires an authenticated session.

        // TODO(week-2): GET /bff/user - the current session as JSON, or 401 when anonymous.
        // Allow anonymous: "am I signed in?" must be answerable by someone who is not.
        //
        // Return only what the UI needs to render itself - a display name, the roles. This is
        // not an authorization boundary and must never be treated as one: it decides which menu
        // items appear, while the API decides what actually happens. A client that hides a button
        // has not secured anything.
        _ = app;
    }
}

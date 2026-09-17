namespace Fleet.Web.Workshop.Extensions;

/// <summary>
/// Everything this host registers, in three parts: a session, a policy, and a proxy.
/// </summary>
/// <remarks>
/// Read <c>TaskTracker/src/TT.Web/Extensions/ServiceCollectionExtensions.cs</c> afterwards if you
/// have it to hand - it is the same three parts against a different identity provider, and the
/// comparison is the point.
/// </remarks>
internal static class ServiceCollectionExtensions
{
    public static void AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        AddAuthentication(services, configuration);
        AddAuthorization(services);
        AddApiProxy(services, configuration);
    }

    // -------------------------------------------------------------------------
    // Authentication: a cookie session in front, an OIDC code flow behind it.
    // -------------------------------------------------------------------------

    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        // TODO(week-2): bind OidcSettings from the Authentication:Oidc section and throw if it is
        // missing. A host that starts with no identity provider configured and only fails at the
        // first sign-in is a host that fails in front of a user rather than in front of you.

        // TODO(week-2): AddAuthentication with three schemes that are not the same thing:
        //   DefaultScheme          the cookie  - how an *existing* session is read
        //   DefaultChallengeScheme OIDC        - what happens when there isn't one
        //   DefaultSignOutScheme   OIDC        - so signing out here also signs out of Keycloak
        //
        // Getting DefaultScheme wrong is the classic BFF bug: every request re-challenges, the
        // user bounces to Keycloak on every click, and the session appears never to stick.

        // TODO(week-2): AddCookie. Four settings decide whether this is a session or a liability:
        //   HttpOnly     - JavaScript must not be able to read it. This is the whole point.
        //   SameSite     - Lax. The OIDC callback is a top-level GET redirect, and Lax sends the
        //                  cookie on those while still blocking cross-site POSTs.
        //   SecurePolicy - SameAsRequest locally (Keycloak is plain HTTP); Always in production.
        //   Name         - anything stable. The __Host- prefix buys real guarantees over HTTPS.
        //
        // And two events, which are the reason a SPA can use this at all:
        //   OnRedirectToLogin        -> 401, never a redirect to an HTML login page
        //   OnRedirectToAccessDenied -> 403
        // An XHR that receives a 302 to Keycloak cannot follow it usefully; it either fails CORS
        // or silently returns Keycloak's HTML as if it were your JSON. The SPA needs a status code
        // it can branch on - which is exactly what src/auth/useUser.ts does with the 401.

        // TODO(week-2): AddOpenIdConnect against the fleet realm.
        //   ResponseType = code                  authorization code flow, never implicit
        //   ResponseMode = query                 keeps the callback a top-level GET so Lax works
        //   SaveTokens   = true                  the proxy needs the access token later
        //   Scope        = from configuration    openid, profile, and whatever the API audience needs
        //
        // Note what is NOT here: no token ever reaches the browser. The cookie is the credential
        // on that side; the token only exists between this host and the API.
        _ = services;
        _ = configuration;
    }

    // -------------------------------------------------------------------------
    // Authorization: one policy, for the proxy route. See Auth/PolicyNames.cs.
    // -------------------------------------------------------------------------

    private static void AddAuthorization(IServiceCollection services)
    {
        // TODO(week-2): add the ApiProxy policy - authenticated user, pinned to the cookie scheme.
        //
        // Pinning the scheme is what makes an anonymous XHR fail fast with 401 (through
        // OnRedirectToLogin above) instead of being challenged into an OIDC redirect it cannot
        // follow. Leave the scheme unpinned and the failure mode is confusing rather than loud.
        _ = services;
    }

    // -------------------------------------------------------------------------
    // Reverse proxy: /api/** -> the Fleet API, with the user's access token attached.
    // -------------------------------------------------------------------------

    private static void AddApiProxy(IServiceCollection services, IConfiguration configuration)
    {
        // TODO(week-2): AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"))
        // and add a request transform that reads the access token out of the session
        // (HttpContext.GetTokenAsync("access_token")) and puts it on the outbound request as
        // Authorization: Bearer.
        //
        // This transform is the join between the two halves of the whole course: the browser
        // holds a cookie, the Fleet API you wrote in week 7 of the API course accepts a bearer
        // token, and this is the one place that turns one into the other.
        _ = services;
        _ = configuration;
    }
}

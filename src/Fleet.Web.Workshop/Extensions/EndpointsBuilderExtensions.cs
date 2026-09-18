using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

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
        // Keycloak redirects the browser back to whatever is registered as the client's
        // redirect/post-logout URI - this host, always, never Vite. A *relative* RedirectUri
        // from there would resolve against this host too, and in development this host has no
        // SPA to show. Anchor it at Vite's origin in development; in production there is only
        // one origin, this is empty, and every redirect stays relative exactly as before.
        var spaOrigin = app.Configuration["Spa:DevServerOrigin"] ?? "";

        app.MapGet("/bff/login", (string? returnUrl) =>
            {
                var path = IsLocalReturnUrl(returnUrl) ? returnUrl! : "/";
                return Results.Challenge(
                    new AuthenticationProperties { RedirectUri = spaOrigin + path },
                    [OpenIdConnectDefaults.AuthenticationScheme]);
            })
            .AllowAnonymous();

        app.MapGet("/bff/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignOutAsync(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = spaOrigin + "/" });
            })
            .RequireAuthorization();

        app.MapGet("/bff/user", (ClaimsPrincipal user) =>
            {
                if (user.Identity is not { IsAuthenticated: true })
                {
                    return Results.Unauthorized();
                }

                var name = user.FindFirst("name")?.Value
                    ?? user.FindFirst("preferred_username")?.Value
                    ?? user.Identity.Name;
                var roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

                return Results.Ok(new { name, roles });
            })
            .AllowAnonymous();
    }

    /// <summary>
    /// Accepts only paths beginning with a single "/". An absolute URL here would turn
    /// <c>/bff/login</c> into an open redirect, and "//evil.example" also begins with "/", so a
    /// leading slash alone is not enough.
    /// </summary>
    private static bool IsLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
}

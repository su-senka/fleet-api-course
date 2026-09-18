using System.Net.Http.Headers;
using Fleet.Web.Workshop.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Yarp.ReverseProxy.Transforms;

namespace Fleet.Web.Workshop.Extensions;

/// <summary>
/// Everything this host registers, in three parts: a session, a policy, and a proxy.
/// </summary>
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
        var oidcSettings = configuration.GetSection(OidcSettings.SectionName).Get<OidcSettings>()
            ?? throw new InvalidOperationException(
                $"Missing required configuration section '{OidcSettings.SectionName}'.");

        var isDevelopment = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"],
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = isDevelopment
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                // The __Host- prefix demands the Secure attribute on every cookie carrying it,
                // which a plain-HTTP local Keycloak round trip cannot satisfy.
                options.Cookie.Name = isDevelopment ? "fleet-web-session" : "__Host-fleet-web-session";

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.MetadataAddress = oidcSettings.MetadataAddress;
                options.ClientId = oidcSettings.ClientId;
                options.ClientSecret = oidcSettings.ClientSecret;
                options.RequireHttpsMetadata = oidcSettings.RequireHttpsMetadata;

                options.ResponseType = "code";
                options.ResponseMode = "query";
                options.SaveTokens = true;

                // These default to Secure regardless of environment, on the assumption that OIDC
                // always runs over HTTPS. Locally it does not: Keycloak's redirect back to
                // /signin-oidc is a plain-HTTP request, a Secure cookie cannot travel on it, and
                // the handler fails with "Correlation failed" before it ever reads the code.
                var cookieSecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.CorrelationCookie.SecurePolicy = cookieSecurePolicy;
                options.NonceCookie.SecurePolicy = cookieSecurePolicy;

                options.Scope.Clear();
                foreach (var scope in oidcSettings.Scopes)
                {
                    options.Scope.Add(scope);
                }
            });
    }

    // -------------------------------------------------------------------------
    // Authorization: one policy, for the proxy route. See Auth/PolicyNames.cs.
    // -------------------------------------------------------------------------

    private static void AddAuthorization(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Pinning the scheme is what makes an anonymous XHR fail fast with 401 (through
            // OnRedirectToLogin above) instead of being challenged into an OIDC redirect it
            // cannot follow.
            options.AddPolicy(PolicyNames.ApiProxy, policy =>
            {
                policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });
        });
    }

    // -------------------------------------------------------------------------
    // Reverse proxy: /api/** -> the Fleet API, with the user's access token attached.
    // -------------------------------------------------------------------------

    private static void AddApiProxy(IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(transformBuilderContext =>
            {
                transformBuilderContext.AddRequestTransform(async transformContext =>
                {
                    var accessToken = await transformContext.HttpContext.GetTokenAsync("access_token");
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        transformContext.ProxyRequest.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", accessToken);
                    }
                });
            });
    }
}

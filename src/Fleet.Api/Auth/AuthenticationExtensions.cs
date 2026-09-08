using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fleet.Api.Auth;

/// <summary>JWT bearer authentication against the Keycloak realm in Compose.</summary>
internal static class AuthenticationExtensions
{
    /// <summary>
    /// Keycloak's own claim for the username. Not <c>sub</c>, which is an opaque GUID, and not
    /// <c>name</c>, which is a display name and not unique.
    /// </summary>
    public const string PreferredUsernameClaim = "preferred_username";

    public static IHostApplicationBuilder AddFleetAuthentication(this IHostApplicationBuilder builder)
    {
        var authority = builder.Configuration["Authentication:Authority"];
        var audience = builder.Configuration["Authentication:Audience"];

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;

                // Keycloak in Compose speaks plain HTTP. This is the line that would be a serious
                // security bug anywhere other than a laptop, which is why it is guarded.
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    // The default five-minute clock skew hides expiry bugs during a workshop.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = PreferredUsernameClaim,
                    RoleClaimType = ClaimTypes.Role,
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        FlattenKeycloakRealmRoles(context.Principal);
                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(FleetPolicies.AnyFleetUser, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(FleetRoles.Admin, FleetRoles.Dispatcher, FleetRoles.Driver))
            .AddPolicy(FleetPolicies.ManageFleet, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(FleetRoles.Admin))
            .AddPolicy(FleetPolicies.ManageBookings, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(FleetRoles.Admin, FleetRoles.Dispatcher));

        return builder;
    }

    /// <summary>
    /// Copies Keycloak's nested realm roles up into ordinary role claims.
    /// </summary>
    /// <remarks>
    /// Keycloak puts them inside a JSON object: <c>"realm_access": { "roles": ["fleet.driver"] }</c>.
    /// ASP.NET Core flattens that into a single claim whose value is the whole JSON blob, so
    /// <c>User.IsInRole("fleet.driver")</c> is false and every role check silently fails. This is
    /// the standard fix, and the reason it is worth reading a decoded token before trusting one.
    /// </remarks>
    private static void FlattenKeycloakRealmRoles(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var realmAccess = principal.FindFirst("realm_access")?.Value;

        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return;
        }

        using var document = System.Text.Json.JsonDocument.Parse(realmAccess);

        if (!document.RootElement.TryGetProperty("roles", out var roles)
            || roles.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return;
        }

        foreach (var role in roles.EnumerateArray())
        {
            var value = role.GetString();

            if (!string.IsNullOrWhiteSpace(value))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, value));
            }
        }
    }

    /// <summary>The signed-in user's Keycloak username, or <c>null</c> when not signed in.</summary>
    public static string? UserName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(PreferredUsernameClaim) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
}

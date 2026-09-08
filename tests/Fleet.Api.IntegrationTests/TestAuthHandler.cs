using System.Security.Claims;
using System.Text.Encodings.Web;
using Fleet.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// Stands in for Keycloak, taking the caller's identity from two request headers.
/// </summary>
/// <remarks>
/// <para>
/// The tests are about the API's behaviour, not about OpenID Connect. Running Keycloak to prove
/// that a driver gets a 403 would add a minute to every run and test somebody else's software.
/// </para>
/// <para>
/// What this <em>cannot</em> test is the part that actually broke first: Keycloak nests realm
/// roles inside a JSON claim, and ASP.NET Core does not unpack them. That is covered by
/// <c>requests/auth.http</c> and by running the thing, not from here.
/// </para>
/// </remarks>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    /// <summary>The Keycloak username to sign in as, matched against <c>drivers.user_id</c>.</summary>
    public const string UserHeader = "X-Test-User";

    /// <summary>Comma-separated <c>fleet.*</c> roles.</summary>
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrWhiteSpace(user))
        {
            // No header means no credentials, which must produce a 401 exactly as a missing bearer
            // token would.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(AuthenticationExtensions.PreferredUsernameClaim, user.ToString()),
            new(ClaimTypes.NameIdentifier, user.ToString()),
        };

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, AuthenticationExtensions.PreferredUsernameClaim, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

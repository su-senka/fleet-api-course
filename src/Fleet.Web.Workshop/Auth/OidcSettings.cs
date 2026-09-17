namespace Fleet.Web.Workshop.Auth;

/// <summary>
/// The OIDC client this host signs users in with, bound from <c>Authentication:Oidc</c>.
/// </summary>
/// <remarks>
/// Configuration-driven on purpose: the same code signs in against Keycloak here and against
/// whatever the customer runs in production, without a rebuild. Nothing in this class is a
/// secret except <see cref="ClientSecret"/>, which belongs in user secrets rather than in
/// <c>appsettings.json</c>.
/// </remarks>
internal sealed class OidcSettings
{
    public const string SectionName = "Authentication:Oidc";

    /// <summary>The discovery document, e.g. the realm's <c>.well-known/openid-configuration</c>.</summary>
    public string MetadataAddress { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    /// <summary>Null for a public client. Keycloak's <c>fleet-web</c> client is confidential.</summary>
    public string? ClientSecret { get; init; }

    public string[] Scopes { get; init; } = [];

    /// <summary>
    /// False only on a laptop. Keycloak in Compose speaks plain HTTP; anywhere else this being
    /// false means tokens are negotiated over a channel anyone can read.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;
}

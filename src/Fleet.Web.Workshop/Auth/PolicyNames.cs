namespace Fleet.Web.Workshop.Auth;

/// <summary>Authorization policies this host applies. There is deliberately only one.</summary>
/// <remarks>
/// A BFF gates one thing: is there a session? Who may do what is the API's decision, and
/// duplicating it here would mean two implementations of one rule - the same argument the API
/// course makes about validators and domain invariants.
/// </remarks>
internal static class PolicyNames
{
    /// <summary>Applied by the YARP route so an anonymous <c>/api</c> call fails fast with 401.</summary>
    public const string ApiProxy = "ApiProxy";
}

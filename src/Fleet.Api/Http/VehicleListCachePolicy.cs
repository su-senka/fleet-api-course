using Microsoft.AspNetCore.OutputCaching;

namespace Fleet.Api.Http;

/// <summary>
/// Caches the vehicle list for a short while, including for signed-in callers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read this before copying it.</b> ASP.NET Core's default output-cache policy refuses to cache
/// any request carrying an <c>Authorization</c> header, and it is right to: caching a response
/// that depends on who asked, and then serving it to somebody else, is one of the more
/// spectacular ways to leak data.
/// </para>
/// <para>
/// This policy overrides that for one endpoint, and only because the vehicle list is genuinely the
/// same for every caller - the same 250 vehicles whoever is signed in. The moment that stops being
/// true, this becomes a security hole rather than an optimisation. The booking list, which differs
/// per driver, is deliberately not cached at all.
/// </para>
/// <para>
/// The safer general answer is <see cref="OutputCacheOptions"/> with <c>SetVaryByHeader</c> on the
/// authorization header, or a shorter cache keyed by role. Both cost more and teach less about
/// why the default exists.
/// </para>
/// </remarks>
internal sealed class VehicleListCachePolicy : IOutputCachePolicy
{
    public const string Name = "vehicle-list";

    /// <summary>Short enough that a newly registered vehicle appears almost at once.</summary>
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    public static readonly VehicleListCachePolicy Instance = new();

    ValueTask IOutputCachePolicy.CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;

        // Only GET, and never a request that wants a fresh answer.
        var cacheable = HttpMethods.IsGet(request.Method);

        context.EnableOutputCaching = cacheable;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = Duration;

        // The query string *is* the identity of this resource: page 2 filtered by depot is not the
        // same response as page 1. Forgetting this is the classic output-caching bug - everybody
        // gets page 1 for thirty seconds.
        context.CacheVaryByRules.QueryKeys = "*";

        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        // Never store anything but a plain 200. Caching a 401 would be memorable.
        if (response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}

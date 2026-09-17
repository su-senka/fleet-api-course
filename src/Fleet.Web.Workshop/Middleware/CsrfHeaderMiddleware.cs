namespace Fleet.Web.Workshop.Middleware;

/// <summary>
/// Anti-CSRF guard for the cookie-authenticated proxy.
/// </summary>
/// <remarks>
/// <para>
/// The moment authentication becomes a cookie, the browser attaches it to <em>every</em> request
/// to this origin - including one triggered by a form on somebody else's site. A token in an
/// <c>Authorization</c> header has this problem by construction; a cookie does not, which is the
/// price of the BFF pattern and the reason this file exists.
/// </para>
/// <para>
/// The defence is a custom request header. A cross-site form post or a top-level navigation
/// cannot set one - only JavaScript running on our own origin can, and the same-origin policy
/// stops anyone else's JavaScript from doing so. So the header's mere presence is the proof.
/// Its value carries no information and does not need to; requiring a specific value would
/// suggest it were a secret, and it is not.
/// </para>
/// </remarks>
internal sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";

    public async Task InvokeAsync(HttpContext context)
    {
        // TODO(week-2): reject any request under /api that does not carry X-CSRF: 1, with a 400
        // and a small JSON body saying which header is missing. Everything else passes through
        // untouched - this guard is about the proxied API, not about static files or /bff.
        //
        // Think about why 400 and not 401 or 403: the caller is not unauthenticated and not
        // forbidden, they have sent a request this host will not process as written.
        await next(context);
    }
}

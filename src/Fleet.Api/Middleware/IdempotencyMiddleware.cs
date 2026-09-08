using System.Security.Cryptography;
using System.Text;
using Fleet.Api.Http;
using Fleet.Common.Idempotency;
using Fleet.Common.Results;

namespace Fleet.Api.Middleware;

/// <summary>
/// Marks an endpoint as honouring the <c>Idempotency-Key</c> header.
/// </summary>
/// <remarks>
/// Endpoint metadata rather than a route check, so the middleware never has to know which routes
/// opted in. Adding it to an endpoint is <c>.WithIdempotency()</c>.
/// </remarks>
internal sealed class IdempotentEndpointAttribute : Attribute;

/// <summary>
/// Replays the response of a repeated request instead of doing the work twice.
/// </summary>
/// <remarks>
/// <para>
/// The problem it solves: a client sends <c>POST /bookings</c>, the response is lost to a flaky
/// connection, and the client retries. Without this, the vehicle is booked twice - or, more
/// likely, the second attempt hits the overlap constraint and the client is told 409 for a booking
/// it successfully made.
/// </para>
/// <para>
/// The client sends <c>Idempotency-Key: &lt;something unique&gt;</c> and repeats it on the retry.
/// The first request claims the key and does the work; the retry finds it claimed and replays the
/// stored response verbatim.
/// </para>
/// <para>
/// Middleware rather than an endpoint filter, because the response body has to be captured, and
/// that means swapping <c>HttpContext.Response.Body</c> before the handler runs. A filter sees the
/// return value but not the bytes.
/// </para>
/// <para>
/// The fingerprint is what stops the dangerous case: a client that reuses a key with a
/// <em>different</em> body is not retrying, it is confused, and replaying an unrelated response
/// would be worse than either succeeding or failing.
/// </para>
/// </remarks>
internal sealed class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private const string HeaderName = "Idempotency-Key";

    /// <summary>Set on a replayed response, so a client can tell it did not do the work twice.</summary>
    private const string ReplayHeaderName = "Idempotency-Replayed";

    private const int MaxKeyLength = 200;

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<IdempotentEndpointAttribute>() is null)
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            // The header is optional. Requiring it would be a defensible API decision - a 400 with
            // "Idempotency-Key is required" - and would push the safety onto every client whether
            // they wanted it or not. This one honours it when offered.
            await next(context);
            return;
        }

        var key = headerValues.ToString().Trim();

        if (key.Length is 0 or > MaxKeyLength)
        {
            await WriteProblemAsync(
                context,
                Error.Validation(
                    "idempotency.key_invalid",
                    $"{HeaderName} must be between 1 and {MaxKeyLength} characters."),
                StatusCodes.Status400BadRequest);

            return;
        }

        var fingerprint = await FingerprintAsync(context.Request);

        if (await store.TryBeginAsync(key, fingerprint, context.RequestAborted))
        {
            await RunAndRecordAsync(context, store, key);
            return;
        }

        await ReplayAsync(context, store, key, fingerprint);
    }

    /// <summary>Runs the endpoint with the response captured, and stores whatever it produced.</summary>
    private async Task RunAndRecordAsync(HttpContext context, IIdempotencyStore store, string key)
    {
        var originalBody = context.Response.Body;

        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Position = 0;
            var body = await new StreamReader(buffer).ReadToEndAsync(context.RequestAborted);

            if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                // A failure on our side is not an outcome worth replaying. Releasing the key lets
                // the client's retry actually retry, instead of being told for ever that a request
                // is in progress.
                await store.AbandonAsync(key, context.RequestAborted);

                logger.LogWarning(
                    "Released idempotency key {Key} after a {StatusCode}", key, context.Response.StatusCode);
            }
            else
            {
                await store.CompleteAsync(key, context.Response.StatusCode, body, context.RequestAborted);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
        }
        catch
        {
            // The endpoint threw, so nothing was stored and the key must not stay claimed.
            await store.AbandonAsync(key, CancellationToken.None);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    /// <summary>Answers a request whose key somebody else already claimed.</summary>
    private async Task ReplayAsync(
        HttpContext context,
        IIdempotencyStore store,
        string key,
        string fingerprint)
    {
        var record = await store.GetAsync(key, context.RequestAborted);

        if (record is null)
        {
            // Claimed and then released between our two calls. Treat it as a fresh request rather
            // than inventing a failure for a race the client cannot see.
            await next(context);
            return;
        }

        if (!string.Equals(record.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            // Same key, different body. That is a bug in the client, and the one case where saying
            // so is far better than guessing which of the two requests it meant.
            await WriteProblemAsync(
                context,
                Error.Validation(
                    "idempotency.key_reused",
                    $"This {HeaderName} was already used for a different request body."),
                StatusCodes.Status422UnprocessableEntity);

            return;
        }

        if (record.State == IdempotencyState.InProgress)
        {
            // The first request is still running. There is no stored response to replay yet, and
            // guessing would be worse than asking the client to wait.
            context.Response.Headers.RetryAfter = "1";

            await WriteProblemAsync(
                context,
                Error.Conflict(
                    "idempotency.in_progress",
                    "A request with this key is still being processed. Try again shortly."),
                StatusCodes.Status409Conflict);

            return;
        }

        logger.LogInformation("Replaying stored response for idempotency key {Key}", key);

        context.Response.StatusCode = record.StatusCode ?? StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        context.Response.Headers[ReplayHeaderName] = "true";

        if (!string.IsNullOrEmpty(record.ResponseBody))
        {
            await context.Response.WriteAsync(record.ResponseBody, context.RequestAborted);
        }
    }

    /// <summary>
    /// Hashes the request body, so a reused key with different content can be spotted.
    /// </summary>
    /// <remarks>
    /// The body has to be buffered first: it is a forward-only stream, and reading it here would
    /// otherwise leave nothing for the model binder.
    /// </remarks>
    private static async Task<string> FingerprintAsync(HttpRequest request)
    {
        request.EnableBuffering();

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(request.Body, request.HttpContext.RequestAborted);

        request.Body.Position = 0;

        return Convert.ToHexStringLower(hash);
    }

    private static async Task WriteProblemAsync(HttpContext context, Error error, int statusCode)
    {
        var result = ProblemResults.From(error, context, statusCode);
        await result.ExecuteAsync(context);
    }
}

internal static class IdempotencyExtensions
{
    /// <summary>Honours <c>Idempotency-Key</c> on this endpoint.</summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new IdempotentEndpointAttribute());

    public static IApplicationBuilder UseFleetIdempotency(this IApplicationBuilder app) =>
        app.UseMiddleware<IdempotencyMiddleware>();
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fleet.Common.Results;
using Microsoft.Extensions.Logging;

namespace Fleet.Modules.Maintenance.Supplier;

/// <summary>
/// A typed <c>HttpClient</c> for the parts supplier, with no resilience whatsoever.
/// </summary>
/// <remarks>
/// <para>
/// <b>This client is deliberately naive, and it is the week-12 assignment to fix it.</b> It has no
/// timeout beyond <c>HttpClient</c>'s hundred-second default, no retry, no circuit breaker and no
/// bulkhead. The supplier it talks to returns 500 a quarter of the time and stalls for eight
/// seconds a sixth of the time.
/// </para>
/// <para>
/// Work out what that costs before you fix it. A request that stalls for eight seconds holds a
/// connection, a request thread and whatever the caller was holding open. Enough of them at once
/// and an API that is perfectly healthy stops answering anything at all - including the endpoints
/// that never touch the supplier.
/// </para>
/// <para>
/// What this class <em>does</em> do properly is turn failure into a
/// <see cref="Result{T}"/>. Whether to retry is a policy question; whether an upstream 500 should
/// surface as an exception is not, and the answer is no.
/// </para>
/// </remarks>
internal sealed class SupplierClient(HttpClient httpClient, ILogger<SupplierClient> logger) : ISupplierClient
{
    public async Task<Result<SupplierOrderResponse>> PlaceOrderAsync(
        Guid workOrderId,
        IReadOnlyList<SupplierOrderLine> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var request = new SupplierOrderRequest(workOrderId, lines);

        try
        {
            using var response = await httpClient.PostAsJsonAsync("orders", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var confirmation = await response.Content
                    .ReadFromJsonAsync<SupplierOrderResponse>(cancellationToken);

                if (confirmation is null || string.IsNullOrWhiteSpace(confirmation.OrderId))
                {
                    logger.LogWarning(
                        "Supplier accepted work order {WorkOrderId} but returned no order id", workOrderId);

                    return Error.Unavailable(
                        "supplier.empty_response", "The supplier accepted the order but returned no order id.");
                }

                logger.LogInformation(
                    "Supplier accepted work order {WorkOrderId} as order {SupplierOrderId}",
                    workOrderId,
                    confirmation.OrderId);

                return confirmation;
            }

            return await FailureFromResponseAsync(response, workOrderId, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Connection refused, DNS failure, socket reset. The supplier is not there at all.
            logger.LogWarning(
                exception, "Could not reach the supplier for work order {WorkOrderId}", workOrderId);

            return Error.Unavailable("supplier.unreachable", "The parts supplier could not be reached.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation. Distinguishing it from the
            // caller genuinely giving up is the awkward part, and this is the usual way.
            logger.LogWarning(
                exception, "The supplier timed out for work order {WorkOrderId}", workOrderId);

            return Error.Unavailable("supplier.timeout", "The parts supplier did not respond in time.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception, "The supplier returned something that was not JSON for {WorkOrderId}", workOrderId);

            return Error.Unavailable("supplier.bad_response", "The parts supplier returned an unreadable response.");
        }
    }

    private async Task<Error> FailureFromResponseAsync(
        HttpResponseMessage response,
        Guid workOrderId,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        logger.LogWarning(
            "Supplier refused work order {WorkOrderId} with {StatusCode}: {Body}",
            workOrderId,
            (int)response.StatusCode,
            body);

        return response.StatusCode switch
        {
            // The supplier is telling us to slow down, and its Retry-After says by how much. Note
            // that nothing here acts on that header - honouring it is part of the week-12 work.
            HttpStatusCode.TooManyRequests => Error.Unavailable(
                "supplier.rate_limited",
                RetryAfterMessage(response, "The parts supplier is rate-limiting us.")),

            HttpStatusCode.ServiceUnavailable => Error.Unavailable(
                "supplier.outage",
                RetryAfterMessage(response, "The parts supplier is temporarily unavailable.")),

            // Our fault, not theirs. Retrying an order the supplier considers malformed just
            // produces the same answer more times.
            HttpStatusCode.BadRequest => Error.Validation(
                "supplier.rejected_order", "The parts supplier rejected the order as invalid."),

            _ => Error.Unavailable(
                "supplier.error", $"The parts supplier returned {(int)response.StatusCode}."),
        };
    }

    private static string RetryAfterMessage(HttpResponseMessage response, string message) =>
        response.Headers.RetryAfter?.Delta is { } delta
            ? $"{message} Try again in {delta.TotalSeconds:0} seconds."
            : message;
}

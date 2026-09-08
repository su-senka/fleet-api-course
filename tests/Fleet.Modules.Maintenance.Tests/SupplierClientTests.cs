using System.Net;
using System.Net.Http.Json;
using System.Text;
using Fleet.Common.Results;
using Fleet.Modules.Maintenance.Supplier;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fleet.Modules.Maintenance.Tests;

/// <summary>
/// How the supplier client turns a bad day upstream into an ordinary error.
/// </summary>
/// <remarks>
/// <para>
/// The client has no retry and no circuit breaker, so there is no policy behaviour to test - which
/// is the point. What there <em>is</em> to test is that none of these failures escapes as an
/// exception, because an unhandled <c>HttpRequestException</c> is a 500 for a problem that is not
/// the caller's fault and might well fix itself.
/// </para>
/// <para>
/// Driven by a stub <c>HttpMessageHandler</c> rather than by the real fake supplier: these are
/// about the mapping, and a test that needed a quarter of its runs to fail randomly would be
/// worse than useless.
/// </para>
/// </remarks>
public sealed class SupplierClientTests
{
    private static readonly Guid WorkOrderId = Guid.CreateVersion7();

    private static readonly SupplierOrderLine[] Lines = [new("OLEJ-FILTR", 2)];

    private static SupplierClient ClientFor(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClient = new HttpClient(new StubHandler(respond))
        {
            BaseAddress = new Uri("http://supplier.test/"),
        };

        return new SupplierClient(httpClient, NullLogger<SupplierClient>.Instance);
    }

    [Fact]
    public async Task A_successful_order_returns_the_supplier_order_id()
    {
        var client = ClientFor(_ => JsonResponse(HttpStatusCode.Created, new
        {
            orderId = "SUP-000042",
            workOrderId = WorkOrderId,
            status = "Accepted",
            receivedAt = DateTimeOffset.UtcNow,
        }));

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("SUP-000042", result.Value.OrderId);
    }

    [Fact]
    public async Task A_server_error_becomes_unavailable_rather_than_an_exception()
    {
        var client = ClientFor(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\":\"fell over\"}", Encoding.UTF8, "application/json"),
        });

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Unavailable, result.Error.Kind);
        Assert.Equal("supplier.error", result.Error.Code);
    }

    [Fact]
    public async Task A_rate_limit_becomes_unavailable_and_reports_the_retry_after()
    {
        var client = ClientFor(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":\"too many\"}", Encoding.UTF8, "application/json"),
            };

            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(42));

            return response;
        });

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Unavailable, result.Error.Kind);
        Assert.Equal("supplier.rate_limited", result.Error.Code);

        // The client surfaces what the supplier said. Acting on it is week-12's job.
        Assert.Contains("42 seconds", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outage_becomes_unavailable()
    {
        var client = ClientFor(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{\"error\":\"down\"}", Encoding.UTF8, "application/json"),
        });

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Unavailable, result.Error!.Kind);
        Assert.Equal("supplier.outage", result.Error.Code);
    }

    [Fact]
    public async Task A_rejected_order_is_our_fault_and_maps_to_validation()
    {
        // A 400 means the supplier understood us and disagreed. Retrying it changes nothing, which
        // is exactly why it must not be classified alongside the transient failures.
        var client = ClientFor(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"no lines\"}", Encoding.UTF8, "application/json"),
        });

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("supplier.rejected_order", result.Error.Code);
    }

    [Fact]
    public async Task An_unreachable_supplier_becomes_unavailable()
    {
        var client = ClientFor(_ => throw new HttpRequestException("Connection refused"));

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Unavailable, result.Error!.Kind);
        Assert.Equal("supplier.unreachable", result.Error.Code);
    }

    [Fact]
    public async Task A_timeout_becomes_unavailable()
    {
        // HttpClient reports its own timeout as a TaskCanceledException, which is easy to confuse
        // with the caller cancelling. The client tells them apart by the cancellation token.
        var client = ClientFor(_ => throw new TaskCanceledException("The request timed out."));

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Unavailable, result.Error!.Kind);
        Assert.Equal("supplier.timeout", result.Error.Code);
    }

    [Fact]
    public async Task A_success_with_no_order_id_is_treated_as_a_failure()
    {
        // A 201 whose body says nothing useful is not a success in any sense we care about.
        var client = ClientFor(_ => JsonResponse(HttpStatusCode.Created, new
        {
            orderId = "",
            workOrderId = WorkOrderId,
            status = "Accepted",
            receivedAt = DateTimeOffset.UtcNow,
        }));

        var result = await client.PlaceOrderAsync(WorkOrderId, Lines, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Unavailable, result.Error!.Kind);
        Assert.Equal("supplier.empty_response", result.Error.Code);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_swallowed()
    {
        // If the caller gave up, that is not the supplier's failure and must not be reported as
        // one. The request should propagate the cancellation.
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var client = ClientFor(_ => throw new TaskCanceledException("cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.PlaceOrderAsync(WorkOrderId, Lines, cancellation.Token));
    }

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode status, T body) =>
        new(status) { Content = JsonContent.Create(body) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}

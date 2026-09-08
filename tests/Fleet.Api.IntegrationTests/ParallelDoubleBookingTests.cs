using System.Net;
using Fleet.Common.Paging;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// Two clients booking the same vehicle for the same window at the same moment.
/// </summary>
/// <remarks>
/// <para>
/// The module tests already prove the exclusion constraint holds under concurrency. This one
/// proves the rest of the path: that the loser's failure survives the journey out through the
/// service, the endpoint and the problem mapper, and reaches the client as a 409 with a code it
/// can act on - rather than as a 500, or as a second booking.
/// </para>
/// <para>
/// It is the test most worth running after any change to <c>BookingService</c> or to the problem
/// mapping, because every layer has to behave for it to pass.
/// </para>
/// </remarks>
public sealed class ParallelDoubleBookingTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_simultaneous_bookings_produce_exactly_one_201_and_one_409()
    {
        factory.SkipIfUnavailable();

        var (vehicleId, driverId) = await BookableAsync(0);
        var start = Window(500);

        // A barrier so both requests reach the database together. Without it the first finishes
        // before the second starts, the service's own pre-check catches the clash, and the race
        // this test exists for never happens.
        using var gate = new Barrier(2);

        async Task<HttpResponseMessage> BookAsync()
        {
            using var client = factory.DispatcherClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "/bookings")
                .WithJson(new
                {
                    vehicleId,
                    driverId,
                    purpose = "Parallel double booking",
                    startsAt = start,
                    endsAt = start.AddHours(4),
                });

            gate.SignalAndWait();

            return await client.SendAsync(request, Token);
        }

        var responses = await Task.WhenAll(Task.Run(BookAsync), Task.Run(BookAsync));

        try
        {
            var created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
            var conflicted = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();

            if (created.Count != 1 || conflicted.Count != 1)
            {
                var detail = new List<string>();

                foreach (var response in responses)
                {
                    detail.Add($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Token)}");
                }

                Assert.Fail("Expected exactly one 201 and one 409.\n" + string.Join("\n", detail));
            }

            var problem = await conflicted[0].Content.ReadAsStringAsync(Token);

            // Not a 500. The loser is told something actionable, in the same problem format as
            // every other failure.
            //
            // Two codes are acceptable, and the difference is worth knowing. Usually the second
            // insert is cleanly rejected by the exclusion constraint. Occasionally the two
            // transactions deadlock on it instead and Postgres kills one - which means the same
            // thing to the caller and carries a different code so it can be told apart.
            Assert.True(
                problem.Contains("booking.overlaps_existing", StringComparison.Ordinal)
                || problem.Contains("booking.write_conflict", StringComparison.Ordinal),
                $"The loser should be told why in a way it can act on. Got: {problem}");

            Assert.Equal("application/problem+json", conflicted[0].Content.Headers.ContentType?.MediaType);

            // And the database really does hold one booking, not two.
            using var client = factory.DispatcherClient();

            var forWindow = await (await client.GetAsync(
                    $"/bookings?vehicleId={vehicleId}"
                    + $"&from={Uri.EscapeDataString(start.ToString("O"))}"
                    + $"&to={Uri.EscapeDataString(start.AddHours(4).ToString("O"))}", Token))
                .ReadAsync<PagedResult<BookingDto>>(Token);

            Assert.Equal(1, forWindow.TotalCount);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Ten_simultaneous_bookings_still_produce_exactly_one_success()
    {
        // Two is the interesting case; ten is the one that catches a fix that only narrowed the
        // window rather than closing it.
        factory.SkipIfUnavailable();

        const int Attempts = 10;

        var (vehicleId, driverId) = await BookableAsync(1);
        var start = Window(520);

        using var gate = new Barrier(Attempts);

        async Task<(HttpStatusCode Status, string Body)> BookAsync()
        {
            using var client = factory.DispatcherClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "/bookings")
                .WithJson(new
                {
                    vehicleId,
                    driverId,
                    purpose = "Ten at once",
                    startsAt = start,
                    endsAt = start.AddHours(4),
                });

            gate.SignalAndWait();

            using var response = await client.SendAsync(request, Token);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(Token));
        }

        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, Attempts).Select(_ => Task.Run(BookAsync)));

        var summary = string.Join(
            "\n", outcomes.Select(o => $"{(int)o.Status}: {o.Body}"));

        Assert.True(
            outcomes.Count(o => o.Status == HttpStatusCode.Created) == 1,
            $"Exactly one request must succeed.\n{summary}");

        // Every loser gets a 4xx it can act on. The distinction that matters is that none of them
        // gets a 500: contention is an expected outcome of a shared calendar, not a fault.
        Assert.True(
            outcomes.All(o => o.Status == HttpStatusCode.Created || (int)o.Status is >= 400 and < 500),
            $"No request may fail with a 5xx.\n{summary}");
    }

    [Fact]
    public async Task Two_simultaneous_retries_of_one_request_book_the_vehicle_once()
    {
        // The same collision seen from the client's side: not two people competing, but one
        // client retrying a request whose response was lost. The Idempotency-Key is what turns
        // this from a 409 into the original 201, replayed.
        factory.SkipIfUnavailable();

        var (vehicleId, driverId) = await BookableAsync(2);
        var start = Window(540);
        var key = $"parallel-{Guid.CreateVersion7()}";

        using var gate = new Barrier(2);

        async Task<HttpStatusCode> BookAsync()
        {
            using var client = factory.DispatcherClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "/bookings")
                .WithJson(new
                {
                    vehicleId,
                    driverId,
                    purpose = "Retried under the same key",
                    startsAt = start,
                    endsAt = start.AddHours(4),
                });

            request.Headers.Add("Idempotency-Key", key);
            gate.SignalAndWait();

            using var response = await client.SendAsync(request, Token);
            return response.StatusCode;
        }

        var statuses = await Task.WhenAll(Task.Run(BookAsync), Task.Run(BookAsync));

        // One does the work. The other either replays the stored 201 or, if it arrived while the
        // first was still running, is told 409 idempotency.in_progress and should retry. Both are
        // correct; what must never happen is two bookings.
        Assert.Contains(HttpStatusCode.Created, statuses);

        using var client = factory.DispatcherClient();

        var forWindow = await (await client.GetAsync(
                $"/bookings?vehicleId={vehicleId}"
                + $"&from={Uri.EscapeDataString(start.ToString("O"))}"
                + $"&to={Uri.EscapeDataString(start.AddHours(4).ToString("O"))}", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        Assert.Equal(1, forWindow.TotalCount);
    }

    private static DateTimeOffset Window(int daysAhead) =>
        new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero)
            .AddDays(daysAhead)
            .AddHours(9);

    private async Task<(Guid VehicleId, Guid DriverId)> BookableAsync(int testIndex)
    {
        using var client = factory.DispatcherClient();

        var vehicles = await (await client.GetAsync("/vehicles?status=Available&pageSize=100", Token))
            .ReadAsync<PagedResult<VehicleDto>>(Token);

        using var driverClient = factory.DriverClient();

        var mine = await (await driverClient.GetAsync("/bookings?pageSize=1", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        return (vehicles.Items[testIndex % vehicles.Items.Count].Id, mine.Items[0].DriverId);
    }
}

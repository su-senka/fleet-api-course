using System.Net;
using Fleet.Common.Paging;
using Fleet.Modules.Bookings.Contracts;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// The Bookings endpoints: ETags, preconditions, idempotency and resource authorization, over
/// real HTTP against a real Postgres.
/// </summary>
public sealed class BookingEndpointTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A window far enough into the future to miss the seed, which fills the calendar for the
    /// next couple of months. Explicitly UTC, so a daylight-saving boundary cannot move it.
    /// </summary>
    private static DateTimeOffset WindowFor(int testIndex) =>
        new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero)
            .AddDays(400 + (testIndex * 7))
            .AddHours(9);

    [Fact]
    public async Task Without_a_token_the_api_says_401()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AnonymousClient();

        using var response = await client.GetAsync("/bookings", Token);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized, Token);
    }

    [Fact]
    public async Task A_get_returns_an_etag()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var page = await (await client.GetAsync("/bookings?pageSize=1", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        using var response = await client.GetAsync($"/bookings/{page.Items[0].Id}", Token);

        await response.ShouldBeAsync(HttpStatusCode.OK, Token);
        Assert.NotNull(response.Headers.ETag);
        Assert.False(response.Headers.ETag!.IsWeak, "If-Match needs a strong ETag.");
    }

    [Fact]
    public async Task A_driver_sees_only_their_own_bookings()
    {
        factory.SkipIfUnavailable();
        using var dispatcher = factory.DispatcherClient();
        using var driver = factory.DriverClient();

        var all = await (await dispatcher.GetAsync("/bookings?pageSize=1", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        var mine = await (await driver.GetAsync("/bookings?pageSize=100", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        Assert.True(mine.TotalCount > 0, "The seeded driver should have some bookings.");
        Assert.True(mine.TotalCount < all.TotalCount, "A driver must not see the whole fleet's calendar.");

        // The count itself must be right, not just the rows. Filtering after the query would
        // leak the true total through the pagination metadata.
        Assert.All(mine.Items, booking => Assert.Equal("Martin Dvorak", booking.DriverName));
    }

    [Fact]
    public async Task A_driver_cannot_read_someone_elses_booking()
    {
        factory.SkipIfUnavailable();
        using var dispatcher = factory.DispatcherClient();
        using var driver = factory.DriverClient();

        var all = await (await dispatcher.GetAsync("/bookings?pageSize=100", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        var somebodyElses = all.Items.First(booking => booking.DriverName != "Martin Dvorak");

        using var asDriver = await driver.GetAsync($"/bookings/{somebodyElses.Id}", Token);
        using var asDispatcher = await dispatcher.GetAsync($"/bookings/{somebodyElses.Id}", Token);

        await asDriver.ShouldBeAsync(HttpStatusCode.Forbidden, Token);
        await asDispatcher.ShouldBeAsync(HttpStatusCode.OK, Token);
    }

    [Fact]
    public async Task Booking_a_vehicle_returns_201_with_a_location_and_an_etag()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 1);
        var start = WindowFor(1);

        using var response = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4));

        await response.ShouldBeAsync(HttpStatusCode.Created, Token);
        Assert.NotNull(response.Headers.Location);
        Assert.NotNull(response.Headers.ETag);

        using var followed = await client.GetAsync(response.Headers.Location, Token);
        await followed.ShouldBeAsync(HttpStatusCode.OK, Token);
    }

    [Fact]
    public async Task An_overlapping_booking_is_a_409()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 2);
        var start = WindowFor(2);

        using var first = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(6));
        await first.ShouldBeAsync(HttpStatusCode.Created, Token);

        using var clash = await CreateAsync(client, vehicleId, driverId, start.AddHours(2), start.AddHours(8));
        var body = await clash.Content.ReadAsStringAsync(Token);

        await clash.ShouldBeAsync(HttpStatusCode.Conflict, Token);
        Assert.Contains("booking.overlaps_existing", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bookings_that_touch_but_do_not_overlap_are_both_accepted()
    {
        // The edge case the seed data also contains, here end to end: the second booking starts
        // at the exact instant the first ends.
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 3);
        var start = WindowFor(3);
        var handover = start.AddHours(4);

        using var first = await CreateAsync(client, vehicleId, driverId, start, handover);
        using var second = await CreateAsync(client, vehicleId, driverId, handover, handover.AddHours(4));

        await first.ShouldBeAsync(HttpStatusCode.Created, Token);
        await second.ShouldBeAsync(HttpStatusCode.Created, Token);
    }

    [Fact]
    public async Task An_idempotency_key_replays_the_first_response()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 4);
        var start = WindowFor(4);
        var key = $"test-{Guid.CreateVersion7()}";

        using var first = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4), key);
        var created = await first.ReadAsync<BookingDto>(Token);

        using var replay = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4), key);
        var replayed = await replay.ReadAsync<BookingDto>(Token);

        await first.ShouldBeAsync(HttpStatusCode.Created, Token);
        await replay.ShouldBeAsync(HttpStatusCode.Created, Token);
        Assert.Equal(created.Id, replayed.Id);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));

        // And only one booking was actually made, which is the entire point.
        // Note Uri.EscapeDataString: an ISO-8601 offset contains a +, and an un-encoded + in a
        // query string arrives as a space. The API now answers 400 for that rather than quietly
        // ignoring the filter, which is how this test caught it.
        var forVehicle = await (await client.GetAsync(
                $"/bookings?vehicleId={vehicleId}"
                + $"&from={Uri.EscapeDataString(start.ToString("O"))}"
                + $"&to={Uri.EscapeDataString(start.AddHours(4).ToString("O"))}", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        Assert.Equal(1, forVehicle.TotalCount);
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_with_a_different_body_is_a_422()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 5);
        var start = WindowFor(5);
        var key = $"test-{Guid.CreateVersion7()}";

        using var first = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4), key);
        await first.ShouldBeAsync(HttpStatusCode.Created, Token);

        using var different = await CreateAsync(
            client, vehicleId, driverId, start, start.AddHours(4), key, purpose: "Something else entirely");

        await different.ShouldBeAsync(HttpStatusCode.UnprocessableEntity, Token);
    }

    [Fact]
    public async Task Rescheduling_without_if_match_is_a_428()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 6);
        var start = WindowFor(6);

        var booking = await (await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4)))
            .ReadAsync<BookingDto>(Token);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/bookings/{booking.Id}/schedule")
            .WithJson(new { startsAt = start.AddDays(1), endsAt = start.AddDays(1).AddHours(4) });

        using var response = await client.SendAsync(request, Token);

        await response.ShouldBeAsync(HttpStatusCode.PreconditionRequired, Token);
    }

    [Fact]
    public async Task A_stale_if_match_is_a_412_and_a_fresh_one_succeeds()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 7);
        var start = WindowFor(7);

        using var created = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4));
        var booking = await created.ReadAsync<BookingDto>(Token);
        var etag = created.ETag();

        // Stale: a version this API never issued for this row.
        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/bookings/{booking.Id}/schedule")
            .WithJson(new { startsAt = start.AddDays(1), endsAt = start.AddDays(1).AddHours(4) });
        stale.SetIfMatch("\"AAAAAQ==\"");

        using var staleResponse = await client.SendAsync(stale, Token);
        await staleResponse.ShouldBeAsync(HttpStatusCode.PreconditionFailed, Token);

        // Fresh: succeeds, and the version moves on.
        using var fresh = new HttpRequestMessage(HttpMethod.Put, $"/bookings/{booking.Id}/schedule")
            .WithJson(new { startsAt = start.AddDays(1), endsAt = start.AddDays(1).AddHours(4) });
        fresh.SetIfMatch(etag);

        using var freshResponse = await client.SendAsync(fresh, Token);
        await freshResponse.ShouldBeAsync(HttpStatusCode.OK, Token);
        Assert.NotEqual(etag, freshResponse.ETag());

        // Replaying the now-stale version must fail, or nothing has been gained.
        using var replay = new HttpRequestMessage(HttpMethod.Put, $"/bookings/{booking.Id}/schedule")
            .WithJson(new { startsAt = start.AddDays(2), endsAt = start.AddDays(2).AddHours(4) });
        replay.SetIfMatch(etag);

        using var replayResponse = await client.SendAsync(replay, Token);
        await replayResponse.ShouldBeAsync(HttpStatusCode.PreconditionFailed, Token);
    }

    [Fact]
    public async Task Cancelling_is_idempotent_over_http_even_though_the_domain_calls_it_a_conflict()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 8);
        var start = WindowFor(8);

        using var created = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4));
        var booking = await created.ReadAsync<BookingDto>(Token);

        using var first = new HttpRequestMessage(HttpMethod.Delete, $"/bookings/{booking.Id}");
        first.SetIfMatch(created.ETag());
        using var firstResponse = await client.SendAsync(first, Token);

        await firstResponse.ShouldBeAsync(HttpStatusCode.NoContent, Token);

        using var reread = await client.GetAsync($"/bookings/{booking.Id}", Token);

        using var second = new HttpRequestMessage(HttpMethod.Delete, $"/bookings/{booking.Id}");
        second.SetIfMatch(reread.ETag());
        using var secondResponse = await client.SendAsync(second, Token);

        // 204 again. The service returns Conflict; the endpoint decides that deleting something
        // twice is not an error over HTTP.
        await secondResponse.ShouldBeAsync(HttpStatusCode.NoContent, Token);
    }

    [Fact]
    public async Task Cancelling_frees_the_window()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 9);
        var start = WindowFor(9);

        using var created = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4));
        var booking = await created.ReadAsync<BookingDto>(Token);

        using var cancel = new HttpRequestMessage(HttpMethod.Delete, $"/bookings/{booking.Id}");
        cancel.SetIfMatch(created.ETag());
        await client.SendAsync(cancel, Token);

        // The exclusion constraint is partial, so a cancelled booking holds nothing.
        using var rebooked = await CreateAsync(client, vehicleId, driverId, start, start.AddHours(4));

        await rebooked.ShouldBeAsync(HttpStatusCode.Created, Token);
    }

    [Fact]
    public async Task A_driver_cannot_book_for_somebody_else()
    {
        factory.SkipIfUnavailable();
        using var dispatcher = factory.DispatcherClient();
        using var driver = factory.DriverClient();

        var (vehicleId, _) = await BookableAsync(dispatcher, 10);

        var others = await (await dispatcher.GetAsync("/bookings?pageSize=100", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        var otherDriverId = others.Items.First(b => b.DriverName != "Martin Dvorak").DriverId;
        var start = WindowFor(10);

        using var response = await CreateAsync(driver, vehicleId, otherDriverId, start, start.AddHours(4));
        var body = await response.Content.ReadAsStringAsync(Token);

        await response.ShouldBeAsync(HttpStatusCode.Forbidden, Token);
        Assert.Contains("booking.driver_not_self", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreadable_date_filter_is_a_400_rather_than_the_whole_calendar()
    {
        // Silently dropping a filter it cannot parse would answer a request for one day's
        // bookings with every booking in the system, and the client would never find out.
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        using var response = await client.GetAsync("/bookings?from=yesterday", Token);
        var body = await response.Content.ReadAsStringAsync(Token);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest, Token);
        Assert.Contains("booking.from_invalid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_invalid_booking_request_is_a_400_with_per_field_errors()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DispatcherClient();

        var (vehicleId, driverId) = await BookableAsync(client, 11);
        var start = WindowFor(11);

        using var response = await CreateAsync(
            client, vehicleId, driverId, start, start.AddHours(-1), purpose: "");

        var body = await response.Content.ReadAsStringAsync(Token);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest, Token);
        Assert.Contains("\"errors\"", body, StringComparison.Ordinal);
        Assert.Contains("Purpose", body, StringComparison.Ordinal);
        Assert.Contains("EndsAt", body, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// A vehicle nobody else in this class is using, and the seeded driver who can sign in.
    /// </summary>
    /// <remarks>
    /// Each test gets its own vehicle, keyed off <paramref name="testIndex"/>. Sharing one and
    /// spacing the windows apart also works right up until somebody adds a test that reschedules
    /// into a neighbour's slot - and then fails a test that has nothing to do with the change.
    /// There are 250 vehicles; using a different one per test is free.
    /// </remarks>
    private async Task<(Guid VehicleId, Guid DriverId)> BookableAsync(HttpClient client, int testIndex)
    {
        var vehicles = await (await client.GetAsync("/vehicles?status=Available&pageSize=100", Token))
            .ReadAsync<PagedResult<VehicleDto>>(Token);

        using var driverClient = factory.DriverClient();

        var mine = await (await driverClient.GetAsync("/bookings?pageSize=1", Token))
            .ReadAsync<PagedResult<BookingDto>>(Token);

        return (vehicles.Items[testIndex % vehicles.Items.Count].Id, mine.Items[0].DriverId);
    }

    private Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        Guid vehicleId,
        Guid driverId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string? idempotencyKey = null,
        string purpose = "Integration test booking")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/bookings")
            .WithJson(new { vehicleId, driverId, purpose, startsAt, endsAt });

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request, Token);
    }
}

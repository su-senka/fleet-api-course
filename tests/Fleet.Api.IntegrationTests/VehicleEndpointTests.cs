using System.Net;
using Fleet.Common.Paging;
using Fleet.Modules.Vehicles.Contracts;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// The Vehicles endpoints, over real HTTP against a real database.
/// </summary>
/// <remarks>
/// These test the things unit tests cannot: that a query string becomes the right SQL, that an
/// error becomes the right status code, and that authorization is actually applied rather than
/// merely configured.
/// </remarks>
public sealed class VehicleEndpointTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_a_token_the_api_says_401()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AnonymousClient();

        using var response = await client.GetAsync("/vehicles", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_list_is_paged_by_default()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        using var response = await client.GetAsync("/vehicles", Token);
        var page = await response.ReadAsync<PagedResult<VehicleDto>>(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PageRequest.DefaultPageSize, page.Items.Count);

        // At least the 250 seeded vehicles. Not exactly: the tests in this class share one
        // database and one of them registers a vehicle, and xUnit does not promise an order.
        Assert.True(page.TotalCount >= 250, $"Expected at least the seeded 250, saw {page.TotalCount}.");
    }

    [Fact]
    public async Task Page_size_is_clamped_rather_than_rejected()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        using var response = await client.GetAsync("/vehicles?pageSize=5000", Token);
        var page = await response.ReadAsync<PagedResult<VehicleDto>>(Token);

        Assert.Equal(PageRequest.MaxPageSize, page.Items.Count);
    }

    [Fact]
    public async Task Pages_do_not_overlap()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        var first = await (await client.GetAsync("/vehicles?page=1&pageSize=20", Token))
            .ReadAsync<PagedResult<VehicleDto>>(Token);

        var second = await (await client.GetAsync("/vehicles?page=2&pageSize=20", Token))
            .ReadAsync<PagedResult<VehicleDto>>(Token);

        Assert.Empty(first.Items.Select(v => v.Id).Intersect(second.Items.Select(v => v.Id)));
    }

    [Fact]
    public async Task Filtering_by_status_works_and_returns_enums_as_names()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        using var response = await client.GetAsync("/vehicles?status=InMaintenance&pageSize=100", Token);
        var body = await response.Content.ReadAsStringAsync(Token);
        var page = await response.ReadAsync<PagedResult<VehicleDto>>(Token);

        Assert.True(page.TotalCount > 0);
        Assert.All(page.Items, vehicle => Assert.Equal(VehicleStatus.InMaintenance, vehicle.Status));

        // A value read from a response has to be usable as a filter without translation.
        Assert.Contains("\"InMaintenance\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sorting_descending_works()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        var page = await (await client.GetAsync("/vehicles?sort=-odometerKm&pageSize=20", Token))
            .ReadAsync<PagedResult<VehicleDto>>(Token);

        Assert.Equal(
            page.Items.Select(v => v.OdometerKm).OrderDescending(),
            page.Items.Select(v => v.OdometerKm));
    }

    [Fact]
    public async Task An_unknown_sort_field_is_a_400_rather_than_a_different_order()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        using var response = await client.GetAsync("/vehicles?sort=passwordHash", Token);
        var body = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("vehicle.sort_field_unknown", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_id_is_an_rfc_9457_problem_document()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        using var response = await client.GetAsync($"/vehicles/{Guid.CreateVersion7()}", Token);
        var problem = await response.ReadAsync<Dictionary<string, object>>(Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // The four RFC 9457 members plus the code extension a client should branch on.
        Assert.Contains("type", problem);
        Assert.Contains("title", problem);
        Assert.Contains("status", problem);
        Assert.Contains("detail", problem);
        Assert.Equal("vehicle.not_found", problem["code"].ToString());
    }

    [Fact]
    public async Task Registering_a_vehicle_returns_201_and_a_location()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        var depots = await (await client.GetAsync("/depots", Token))
            .ReadAsync<List<DepotDto>>(Token);

        using var response = await client.PostAsync(
            "/vehicles",
            JsonBody(new { plate = $"7ZZ {Random.Shared.Next(1000, 9999)}", type = "Van", depotId = depots[0].Id, odometerKm = 100 }),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        // The Location must actually resolve. A 201 pointing at a 404 is worse than no header.
        using var followed = await client.GetAsync(response.Headers.Location, Token);
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
    }

    [Fact]
    public async Task An_invalid_request_is_a_400_with_per_field_errors()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        using var response = await client.PostAsync(
            "/vehicles",
            JsonBody(new { plate = "", type = "Van", depotId = Guid.CreateVersion7(), odometerKm = -5 }),
            Token);

        var body = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"errors\"", body, StringComparison.Ordinal);
        Assert.Contains("Plate", body, StringComparison.Ordinal);
        Assert.Contains("OdometerKm", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_driver_may_read_the_fleet_but_not_add_to_it()
    {
        factory.SkipIfUnavailable();
        using var client = factory.DriverClient();

        using var read = await client.GetAsync("/vehicles?pageSize=1", Token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var depots = await (await client.GetAsync("/depots", Token)).ReadAsync<List<DepotDto>>(Token);

        using var write = await client.PostAsync(
            "/vehicles",
            JsonBody(new { plate = "1AA 1111", type = "Van", depotId = depots[0].Id, odometerKm = 0 }),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task An_odometer_reading_that_goes_backwards_is_refused()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        var page = await (await client.GetAsync("/vehicles?pageSize=1", Token))
            .ReadAsync<PagedResult<VehicleDto>>(Token);

        var vehicle = page.Items[0];

        using var response = await client.PostAsync(
            $"/vehicles/{vehicle.Id}/odometer-readings",
            JsonBody(new { recordedAt = DateTimeOffset.UtcNow, km = 1 }),
            Token);

        var body = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("odometer.not_monotonic", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_odometer_history_is_paged_newest_first()
    {
        factory.SkipIfUnavailable();
        using var client = factory.AdminClient();

        var vehicles = await (await client.GetAsync("/vehicles?pageSize=1", Token))
            .ReadAsync<PagedResult<VehicleDto>>(Token);

        var readings = await (await client.GetAsync(
                $"/vehicles/{vehicles.Items[0].Id}/odometer-readings?pageSize=5", Token))
            .ReadAsync<PagedResult<OdometerReadingDto>>(Token);

        Assert.Equal(5, readings.Items.Count);
        Assert.True(readings.TotalCount >= 60);
        Assert.Equal(
            readings.Items.Select(r => r.RecordedAt).OrderDescending(),
            readings.Items.Select(r => r.RecordedAt));
    }

    private static StringContent JsonBody(object body) =>
        new(System.Text.Json.JsonSerializer.Serialize(body, FleetApiFactory.Json),
            System.Text.Encoding.UTF8,
            "application/json");
}

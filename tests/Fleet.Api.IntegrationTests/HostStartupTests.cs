using System.Net;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// The cheapest test worth having: the host starts and the container resolves.
/// </summary>
/// <remarks>
/// Most startup mistakes - a missing registration, a badly ordered middleware, a module that
/// wants a service nobody provided - fail here rather than in the first real endpoint test,
/// where the cause is much harder to see.
/// </remarks>
public sealed class HostStartupTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    [Fact]
    public async Task Liveness_probe_answers_without_touching_any_dependency()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Openapi_document_is_served_and_is_empty()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Milestone 6 adds the Vehicles and Bookings paths. Until then the surface really is bare,
        // and this assertion is what will tell us the moment that stops being true.
        Assert.DoesNotContain("/vehicles", document, StringComparison.Ordinal);
    }
}

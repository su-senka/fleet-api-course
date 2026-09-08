using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// Boots <c>Fleet.Api</c> in memory, without Kestrel and without a network port.
/// </summary>
/// <remarks>
/// <para>
/// The infrastructure connection strings are blanked out, and <c>Database:Initialize</c> is turned
/// off so the host does not try to migrate or seed on the way up. That keeps the current tests
/// runnable on a laptop with Docker stopped.
/// </para>
/// <para>
/// Milestone 6 replaces the blanks with a Testcontainers Postgres instance, at which point the
/// tests that exercise real endpoints will need Docker running - and will say so when it is not.
/// </para>
/// </remarks>
public sealed class FleetApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Initialize"] = "false",
                ["ConnectionStrings:Fleet"] = string.Empty,
                ["ConnectionStrings:Blobs"] = string.Empty,
                ["Supplier:BaseUrl"] = string.Empty,
                ["Observability:SeqUrl"] = string.Empty,
                ["Observability:OtlpEndpoint"] = string.Empty,
            }));
    }
}

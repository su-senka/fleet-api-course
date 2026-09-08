using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using Fleet.Api.Auth;
using Fleet.Common.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// Boots <c>Fleet.Api</c> against a throwaway Postgres, with Keycloak swapped for a test scheme.
/// </summary>
/// <remarks>
/// <para>
/// The database is real, because most of what these tests check - a 409 from an exclusion
/// constraint, an ETag built from <c>xmin</c> - only exists in Postgres. Everything else that
/// would need a container is turned off: the outbox sweeper, the report generator and the
/// certificate scanner are all background noise here, and a report that takes twenty seconds is
/// not something a test should sit through.
/// </para>
/// <para>
/// The schema is migrated and seeded once for the whole class, which makes the seed's determinism
/// load-bearing: every test can assume the same 250 vehicles and 60 drivers are there.
/// </para>
/// </remarks>
public sealed class FleetApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("fleet")
        .WithUsername("fleet")
        .WithPassword("fleet")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-U", "fleet"))
        .Build();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private string? _connectionString;

    /// <summary>Why the container could not start, or <c>null</c> when it did.</summary>
    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            _connectionString = _postgres.GetConnectionString();
        }
        catch (Exception exception)
        {
            UnavailableReason = exception.Message;
            return;
        }

        // btree_gist has to exist before the Bookings migration can create its exclusion
        // constraint. docker-compose does this in its init script; here the test has to.
        await _postgres.ExecScriptAsync("CREATE EXTENSION IF NOT EXISTS btree_gist;");

        await using var scope = Services.CreateAsyncScope();

        AssertIsolated(scope.ServiceProvider);

        var initializers = scope.ServiceProvider
            .GetServices<IModuleDatabaseInitializer>()
            .OrderBy(initializer => initializer.Order)
            .ToList();

        foreach (var initializer in initializers)
        {
            await initializer.MigrateAsync();
        }

        foreach (var initializer in initializers)
        {
            await initializer.SeedAsync();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Refuses to run unless the host really did pick up the container's connection string.
    /// </summary>
    /// <remarks>
    /// A test suite that silently writes to the development database is worse than one that fails:
    /// it passes, it corrupts data somebody is using, and the first symptom is an unrelated test
    /// failing for reasons nobody can reproduce. This turns that into an immediate, obvious stop.
    /// </remarks>
    private void AssertIsolated(IServiceProvider services)
    {
        var configured = services.GetRequiredService<IConfiguration>().GetConnectionString("Fleet");

        if (!string.Equals(configured, _connectionString, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The test host is not using the test container's database. It resolved "
                + $"'{configured}' instead of '{_connectionString}'. Refusing to run: these tests "
                + "would otherwise write to whatever database that connection string points at.");
        }
    }

    /// <summary>Skips the calling test when there is no Docker to run Postgres in.</summary>
    public void SkipIfUnavailable()
    {
        if (_connectionString is null)
        {
            Assert.Skip($"Postgres container unavailable, so this test cannot run. {UnavailableReason}");
        }
    }

    /// <summary>A client signed in as one of the seeded Keycloak users.</summary>
    public HttpClient ClientAs(string userName, params string[] roles)
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));

        return client;
    }

    public HttpClient AdminClient() => ClientAs("admin.novak", FleetRoles.Admin);

    public HttpClient DispatcherClient() => ClientAs("dispatch.svoboda", FleetRoles.Dispatcher);

    /// <summary>Martin Dvorak, EMP-1004 - a real row in the seeded drivers table.</summary>
    public HttpClient DriverClient() => ClientAs("driver.dvorak", FleetRoles.Driver);

    /// <summary>A client with no credentials at all.</summary>
    public HttpClient AnonymousClient() => CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting, not ConfigureAppConfiguration.
        //
        // This mattered, and it was not obvious. Under the minimal-hosting model the sources added
        // by ConfigureAppConfiguration are layered *underneath* the application's own
        // appsettings.json, so the connection string here lost to the one in the file - and the
        // whole suite quietly ran against the development database instead of its container. The
        // tests passed, wrote 28 bookings into the dev data, and were only caught because a later
        // test found a window it had itself booked on a previous run.
        //
        // UseSetting writes into the web host's configuration, which wins. AssertIsolated below
        // makes sure of it.
        builder.UseSetting("ConnectionStrings:Fleet", _connectionString ?? string.Empty);
        builder.UseSetting("ConnectionStrings:Blobs", string.Empty);
        builder.UseSetting("Supplier:BaseUrl", string.Empty);
        builder.UseSetting("Observability:SeqUrl", string.Empty);
        builder.UseSetting("Observability:OtlpEndpoint", string.Empty);

        // This factory migrates and seeds itself, in InitializeAsync, so the host must not also
        // try to on the way up.
        builder.UseSetting("Database:Initialize", "false");

        // Background services would otherwise poll a database the test is about to drop.
        builder.UseSetting("Outbox:Enabled", "false");
        builder.UseSetting("Reporting:Enabled", "false");
        builder.UseSetting("Drivers:CertificateExpiry:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // Replace JWT bearer with the header-driven test scheme. Everything downstream -
            // policies, the resource-based handler - is the real thing.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Reporting needs an IBlobStore, and AddFleetInfrastructure only registers the Azure
            // one when a blob connection string is configured. The reference slice serves no
            // blobs, so a dictionary is enough and keeps the tests off Azurite.
            services.AddSingleton<Fleet.Common.Storage.IBlobStore, InMemoryBlobStore>();

            services.AddAuthorization(options =>
                options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                        TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build());
        });
    }
}

internal static class HttpClientJsonExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<T>(json, FleetApiFactory.Json)
            ?? throw new InvalidOperationException($"Could not read a {typeof(T).Name} from: {json}");
    }

    /// <summary>
    /// Asserts the status, and puts the response body in the message when it does not match.
    /// </summary>
    /// <remarks>
    /// "Expected Created, Actual Conflict" tells you nothing. The API always explains itself in an
    /// RFC 9457 document, so a failing test may as well quote it.
    /// </remarks>
    public static async Task ShouldBeAsync(
        this HttpResponseMessage response,
        System.Net.HttpStatusCode expected,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Fail(
            $"Expected {(int)expected} {expected} from "
            + $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}, "
            + $"got {(int)response.StatusCode} {response.StatusCode}.\n{body}");
    }

    /// <summary>The ETag as it should be sent back in <c>If-Match</c>, quotes and all.</summary>
    public static string ETag(this HttpResponseMessage response) =>
        response.Headers.ETag?.ToString()
        ?? throw new InvalidOperationException("The response carried no ETag.");

    public static void SetIfMatch(this HttpRequestMessage request, string etag) =>
        request.Headers.TryAddWithoutValidation("If-Match", etag);

    public static HttpRequestMessage WithJson(this HttpRequestMessage request, object body)
    {
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, FleetApiFactory.Json));

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return request;
    }
}

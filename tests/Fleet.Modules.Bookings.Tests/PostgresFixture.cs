using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;

namespace Fleet.Modules.Bookings.Tests;

/// <summary>
/// A throwaway Postgres for the tests that genuinely need one.
/// </summary>
/// <remarks>
/// <para>
/// Almost every test in this repository runs against plain objects and needs no database. This
/// fixture exists for the handful that test something Postgres does rather than something C# does:
/// the exclusion constraint, and the concurrency token underneath it.
/// </para>
/// <para>
/// When Docker is not available the container fails to start and
/// <see cref="SkipIfUnavailable"/> turns that into a skipped test rather than a red build. A
/// laptop with Docker stopped should still be able to run <c>dotnet test</c>.
/// </para>
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("fleet")
        .WithUsername("fleet")
        .WithPassword("fleet")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-U", "fleet"))
        .Build();

    public string? ConnectionString { get; private set; }

    /// <summary>Why the container could not start, or <c>null</c> when it did.</summary>
    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        catch (Exception exception)
        {
            // Almost always "Docker is not running". Recorded rather than thrown, so that the
            // tests can report it once each instead of failing the whole class.
            UnavailableReason = exception.Message;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Skips the calling test when there is no Docker to run Postgres in.</summary>
    public void SkipIfUnavailable()
    {
        if (ConnectionString is null)
        {
            Assert.Skip($"Postgres container unavailable, so this test cannot run. {UnavailableReason}");
        }
    }
}

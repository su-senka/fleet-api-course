using Fleet.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Maintenance.Tests;

/// <summary>
/// Guards the module's one public seam: <c>AddMaintenanceModule</c>.
/// </summary>
/// <remarks>
/// A module that registers a service depending on something nobody registered fails at the first
/// request, in production, in a stack trace nobody enjoys reading. Validating the container at
/// build time is cheap and catches it here instead.
/// </remarks>
public sealed class MaintenanceModuleRegistrationTests
{
    private static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Fleet"] =
                "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet",
        })
        .Build();

    [Fact]
    public void Registration_builds_a_valid_container()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The same two calls a host makes, in the same order. The shared kernel first, because
        // modules depend on what it registers - IClock, for one.
        services.AddFleetCommon();
        services.AddMaintenanceModule(Configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.NotNull(provider);
    }

    [Fact]
    public void Module_owns_exactly_one_schema()
    {
        Assert.Equal("maintenance", MaintenanceModuleExtensions.SchemaName);
    }
}

using Fleet.Common;
using Fleet.Modules.Bookings;
using Fleet.Modules.Vehicles;
using Microsoft.Extensions.Configuration;
using Fleet.Modules.Drivers;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Modules.Reporting.Tests;

/// <summary>
/// Guards the module's one public seam: <c>AddReportingModule</c>.
/// </summary>
/// <remarks>
/// A module that registers a service depending on something nobody registered fails at the first
/// request, in production, in a stack trace nobody enjoys reading. Validating the container at
/// build time is cheap and catches it here instead.
/// </remarks>
public sealed class ReportingModuleRegistrationTests
{
    private static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Fleet"] =
                "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet",
            ["ConnectionStrings:Blobs"] = "UseDevelopmentStorage=true",
        })
        .Build();

    [Fact]
    public void Registration_builds_a_valid_container()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The same calls a host makes, in the same order. Reporting reads vehicles and bookings
        // and writes the finished CSV to the blob store, so all of that has to be there first.
        services.AddFleetCommon();
        services.AddFleetInfrastructure(Configuration);
        services.AddVehiclesModule(Configuration);
        services.AddBookingsModule(Configuration);
        services.AddDriversModule(Configuration);
        services.AddReportingModule(Configuration);

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
        Assert.Equal("reporting", ReportingModuleExtensions.SchemaName);
    }
}

using Fleet.Common.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Common;

/// <summary>Registers the shared-kernel services that every host needs.</summary>
public static class FleetCommonExtensions
{
    /// <summary>
    /// Adds the handful of cross-cutting services that are not owned by any single module.
    /// Call this once, before the <c>Add*Module</c> calls.
    /// </summary>
    public static IServiceCollection AddFleetCommon(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock>(SystemClock.Instance);

        return services;
    }
}

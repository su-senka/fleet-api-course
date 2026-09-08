using Fleet.Common.Results;

namespace Fleet.Modules.Vehicles.Contracts;

/// <summary>
/// Depots. There are four of them and they do not change, which makes this the gentlest possible
/// resource to put an endpoint in front of.
/// </summary>
public interface IDepotService
{
    /// <summary>Every depot, ordered by name. Small enough that it is not paged.</summary>
    Task<Result<IReadOnlyList<DepotDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<DepotDto>> GetAsync(Guid depotId, CancellationToken cancellationToken = default);
}

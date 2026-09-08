using Fleet.Common.Paging;
using Fleet.Common.Results;

namespace Fleet.Modules.Drivers.Contracts;

/// <summary>
/// The application service behind the Drivers endpoints.
/// </summary>
/// <remarks>
/// Finished and unit-tested, and it returns <see cref="Result{T}"/> rather than status codes.
/// Writing the endpoints over it is your job.
/// </remarks>
public interface IDriverService
{
    /// <summary>
    /// One page of drivers.
    /// </summary>
    /// <param name="filter">
    /// Understands a <c>hasValidLicence</c> term (<c>true</c>/<c>false</c>) and a free-text
    /// <see cref="FilterRequest.Search"/> matched against name and employee number.
    /// </param>
    /// <param name="sort">Understands <c>name</c> and <c>employeeNumber</c>.</param>
    Task<Result<PagedResult<DriverDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>One driver with their certificates.</summary>
    Task<Result<DriverDetailDto>> GetAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a driver. <see cref="ErrorKind.Conflict"/> when the employee number is already taken.
    /// </summary>
    Task<Result<DriverDetailDto>> RegisterAsync(
        RegisterDriverCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a certificate to a driver, replacing any existing one of the same kind.
    /// </summary>
    /// <remarks>
    /// A driver holds at most one licence at a time. Renewing it supersedes the old one rather
    /// than accumulating a second, which is why this is not a plain append.
    /// </remarks>
    Task<Result<CertificateDto>> AddCertificateAsync(
        Guid driverId,
        AddCertificateCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>A driver's certificates, current and superseded, newest expiry first.</summary>
    Task<Result<IReadOnlyList<CertificateDto>>> ListCertificatesAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The certificates expiring within <paramref name="withinDays"/> days of
    /// <paramref name="on"/>, as the events a subscriber would receive.
    /// </summary>
    /// <remarks>
    /// The background service that publishes these arrives with the outbox in milestone 5. The
    /// query itself is finished and tested, because the interesting part - the boundary conditions
    /// around "within 30 days" - has nothing to do with hosting.
    /// </remarks>
    Task<Result<IReadOnlyList<CertificateExpiringSoon>>> FindExpiringCertificatesAsync(
        DateOnly on,
        int withinDays,
        CancellationToken cancellationToken = default);
}

namespace Fleet.Modules.Drivers.Contracts;

/// <summary>A driver as they appear in a list.</summary>
public sealed record DriverDto(
    Guid Id,
    string EmployeeNumber,
    string Name,
    string? UserId,
    bool HasValidLicence);

/// <summary>A driver with their certificates. Returned when fetching one by id.</summary>
public sealed record DriverDetailDto(
    Guid Id,
    string EmployeeNumber,
    string Name,
    string? UserId,
    bool HasValidLicence,
    IReadOnlyList<CertificateDto> Certificates);

/// <summary>
/// One certificate.
/// </summary>
/// <param name="ScanBlobId">
/// Where the scanned document lives in the blob store, or <c>null</c> when nobody has uploaded
/// one. It is a blob id, not a URL: turning it into something a browser can fetch is the API
/// layer's job, and it is a more interesting job than it looks.
/// </param>
public sealed record CertificateDto(
    Guid Id,
    Guid DriverId,
    CertificateKind Kind,
    string Number,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    string? ScanBlobId,
    bool IsExpired);

/// <summary>
/// The narrow view of a driver that other modules are allowed to see.
/// </summary>
/// <remarks>
/// Bookings needs to name a driver and check they may drive. It has no business reading their
/// medical certificate, so that is not on here.
/// </remarks>
public sealed record DriverSummary(Guid Id, string EmployeeNumber, string Name);

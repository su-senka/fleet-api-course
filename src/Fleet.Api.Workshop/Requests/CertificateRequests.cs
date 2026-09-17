using Fleet.Modules.Drivers.Contracts;

namespace Fleet.Api.Workshop.Requests;

/// <summary>
/// Adds a certificate to a driver.
/// </summary>
/// <remarks>
/// Matches <c>AddCertificateCommand</c> field-for-field: the scan, if any, is uploaded separately
/// and this request carries only the blob id it came back with, not the file itself.
/// </remarks>
public sealed record AddCertificateRequest(
    CertificateKind Kind,
    string Number,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    string? ScanBlobId);

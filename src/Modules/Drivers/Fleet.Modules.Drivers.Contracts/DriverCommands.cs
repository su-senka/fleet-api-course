namespace Fleet.Modules.Drivers.Contracts;

/// <summary>
/// Adds a driver.
/// </summary>
/// <param name="UserId">
/// The identity-provider subject this driver signs in as, or <c>null</c> for someone who has no
/// login. It is what lets an authorization handler connect the token in front of it to the row
/// in this table.
/// </param>
public sealed record RegisterDriverCommand(string EmployeeNumber, string Name, string? UserId);

/// <summary>Adds a certificate to a driver.</summary>
public sealed record AddCertificateCommand(
    CertificateKind Kind,
    string Number,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    string? ScanBlobId);

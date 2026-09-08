using Fleet.Common.Results;
using Fleet.Modules.Drivers.Contracts;

namespace Fleet.Modules.Drivers.Domain;

/// <summary>
/// A certificate a driver holds: a licence, a medical, or a dangerous-goods qualification.
/// </summary>
internal sealed class Certificate
{
    private Certificate() => Number = string.Empty;

    private Certificate(
        Guid id,
        Guid driverId,
        CertificateKind kind,
        string number,
        DateOnly issuedOn,
        DateOnly expiresOn,
        string? scanBlobId)
    {
        Id = id;
        DriverId = driverId;
        Kind = kind;
        Number = number;
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        ScanBlobId = scanBlobId;
    }

    public const int NumberMaxLength = 40;

    public Guid Id { get; private set; }

    public Guid DriverId { get; private set; }

    public CertificateKind Kind { get; private set; }

    public string Number { get; private set; }

    public DateOnly IssuedOn { get; private set; }

    /// <summary>
    /// The last day the certificate is valid. Inclusive: a licence expiring today is still valid
    /// today. Whether that is the right reading of "expires on" is exactly the sort of thing worth
    /// pinning down in a test rather than in a comment, so <c>CertificateTests</c> does.
    /// </summary>
    public DateOnly ExpiresOn { get; private set; }

    /// <summary>Blob id of the scanned document, when one has been uploaded.</summary>
    public string? ScanBlobId { get; private set; }

    /// <summary>
    /// When this certificate was superseded by a newer one of the same kind. Superseded
    /// certificates stay in the table as history and never count towards eligibility.
    /// </summary>
    public DateTimeOffset? SupersededAt { get; private set; }

    public bool IsCurrent => SupersededAt is null;

    public static Result<Certificate> Issue(
        Guid id,
        Guid driverId,
        CertificateKind kind,
        string number,
        DateOnly issuedOn,
        DateOnly expiresOn,
        string? scanBlobId)
    {
        var trimmedNumber = number?.Trim() ?? string.Empty;

        if (trimmedNumber.Length == 0)
        {
            return Error.Validation("certificate.number_required", "A certificate needs a number.");
        }

        if (trimmedNumber.Length > NumberMaxLength)
        {
            return Error.Validation(
                "certificate.number_too_long",
                $"A certificate number is at most {NumberMaxLength} characters.");
        }

        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("certificate.kind_unknown", $"'{kind}' is not a certificate kind.");
        }

        if (expiresOn < issuedOn)
        {
            return Error.Validation(
                "certificate.expires_before_issued",
                "A certificate cannot expire before it was issued.");
        }

        return new Certificate(id, driverId, kind, trimmedNumber, issuedOn, expiresOn, scanBlobId);
    }

    /// <summary>Whether the certificate is valid on the given day, ignoring whether it is current.</summary>
    public bool IsValidOn(DateOnly date) => date >= IssuedOn && date <= ExpiresOn;

    public void Supersede(DateTimeOffset at) => SupersededAt = at;

    public void AttachScan(string blobId) => ScanBlobId = blobId;
}

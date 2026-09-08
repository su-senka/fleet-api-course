using Fleet.Common.Results;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Domain;

namespace Fleet.Modules.Drivers.Tests;

public sealed class CertificateTests
{
    private static readonly DateOnly Issued = new(2026, 1, 1);

    [Fact]
    public void A_certificate_cannot_expire_before_it_was_issued()
    {
        var result = Certificate.Issue(
            Guid.CreateVersion7(), Guid.CreateVersion7(), CertificateKind.Licence,
            "L-1", Issued, Issued.AddDays(-1), null);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal("certificate.expires_before_issued", result.Error.Code);
    }

    [Fact]
    public void A_certificate_issued_and_expiring_on_the_same_day_is_allowed()
    {
        // Degenerate but not invalid: a one-day permit is a real thing.
        var result = Certificate.Issue(
            Guid.CreateVersion7(), Guid.CreateVersion7(), CertificateKind.ADR,
            "A-1", Issued, Issued, null);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsValidOn(Issued));
    }

    [Fact]
    public void A_certificate_needs_a_number()
    {
        var result = Certificate.Issue(
            Guid.CreateVersion7(), Guid.CreateVersion7(), CertificateKind.Medical,
            "  ", Issued, Issued.AddYears(1), null);

        Assert.True(result.IsFailure);
        Assert.Equal("certificate.number_required", result.Error.Code);
    }

    [Fact]
    public void A_scan_can_be_attached_after_the_fact()
    {
        // Uploading the scan is a separate request from creating the certificate, because the
        // paperwork usually turns up later than the fact that it exists.
        var certificate = Certificate.Issue(
            Guid.CreateVersion7(), Guid.CreateVersion7(), CertificateKind.Licence,
            "L-1", Issued, Issued.AddYears(10), null).Value;

        Assert.Null(certificate.ScanBlobId);

        certificate.AttachScan("certificates/l-1.pdf");

        Assert.Equal("certificates/l-1.pdf", certificate.ScanBlobId);
    }
}

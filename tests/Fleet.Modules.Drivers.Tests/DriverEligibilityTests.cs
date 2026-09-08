using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Domain;

namespace Fleet.Modules.Drivers.Tests;

/// <summary>
/// The rule that decides whether a booking may name this driver: a current licence, valid on the
/// day in question.
/// </summary>
/// <remarks>
/// The boundary cases are the point. "Expires on the 30th" either includes the 30th or it does
/// not, and whichever you choose, somebody will assume the other - so it is pinned here rather
/// than left to a comment.
/// </remarks>
public sealed class DriverEligibilityTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    private static Driver NewDriver() =>
        Driver.Register(Guid.CreateVersion7(), "EMP-1001", "Martin Dvorak", "driver.dvorak").Value;

    private static void GiveLicence(Driver driver, DateOnly issuedOn, DateOnly expiresOn) =>
        driver.AddCertificate(
            Guid.CreateVersion7(), CertificateKind.Licence, "LICENCE-1", issuedOn, expiresOn, null, Now);

    [Fact]
    public void A_driver_with_no_certificates_at_all_cannot_drive()
    {
        var driver = NewDriver();

        Assert.False(driver.CanDriveOn(Today));
    }

    [Fact]
    public void A_driver_with_a_current_licence_can_drive()
    {
        var driver = NewDriver();
        GiveLicence(driver, Today.AddYears(-5), Today.AddYears(5));

        Assert.True(driver.CanDriveOn(Today));
    }

    [Fact]
    public void A_licence_expiring_today_is_still_valid_today()
    {
        // Inclusive. A licence that says "valid until 15 June" is valid on 15 June.
        var driver = NewDriver();
        GiveLicence(driver, Today.AddYears(-5), Today);

        Assert.True(driver.CanDriveOn(Today));
    }

    [Fact]
    public void A_licence_that_expired_yesterday_is_not_valid_today()
    {
        var driver = NewDriver();
        GiveLicence(driver, Today.AddYears(-5), Today.AddDays(-1));

        Assert.False(driver.CanDriveOn(Today));
    }

    [Fact]
    public void A_licence_issued_tomorrow_is_not_valid_today()
    {
        var driver = NewDriver();
        GiveLicence(driver, Today.AddDays(1), Today.AddYears(5));

        Assert.False(driver.CanDriveOn(Today));
    }

    [Fact]
    public void A_licence_issued_today_is_valid_today()
    {
        var driver = NewDriver();
        GiveLicence(driver, Today, Today.AddYears(5));

        Assert.True(driver.CanDriveOn(Today));
    }

    [Fact]
    public void A_medical_certificate_does_not_stand_in_for_a_licence()
    {
        var driver = NewDriver();
        driver.AddCertificate(
            Guid.CreateVersion7(), CertificateKind.Medical, "MED-1", Today.AddYears(-1), Today.AddYears(1), null, Now);

        Assert.False(driver.CanDriveOn(Today));
    }

    [Fact]
    public void Renewing_a_licence_supersedes_the_old_one()
    {
        var driver = NewDriver();
        GiveLicence(driver, Today.AddYears(-10), Today.AddDays(-1));

        driver.AddCertificate(
            Guid.CreateVersion7(), CertificateKind.Licence, "LICENCE-2", Today, Today.AddYears(10), null, Now);

        Assert.True(driver.CanDriveOn(Today));
        Assert.Equal(2, driver.Certificates.Count);
        Assert.Single(driver.Certificates, certificate => certificate.IsCurrent);
    }

    [Fact]
    public void A_superseded_licence_does_not_keep_a_driver_on_the_road()
    {
        // The trap: the old licence is still inside its own validity window, but it has been
        // replaced by an expired one. Only the current certificate counts.
        var driver = NewDriver();
        GiveLicence(driver, Today.AddYears(-1), Today.AddYears(5));

        driver.AddCertificate(
            Guid.CreateVersion7(), CertificateKind.Licence, "LICENCE-2", Today.AddYears(-1), Today.AddDays(-1), null, Now);

        Assert.False(driver.CanDriveOn(Today));
    }

    [Fact]
    public void Eligibility_is_asked_about_a_day_not_about_now()
    {
        // A booking three weeks out has to be checked against the licence's state three weeks out.
        var driver = NewDriver();
        GiveLicence(driver, Today.AddYears(-5), Today.AddDays(10));

        Assert.True(driver.CanDriveOn(Today));
        Assert.False(driver.CanDriveOn(Today.AddDays(21)));
    }

    [Fact]
    public void An_ADR_certificate_is_tracked_but_does_not_grant_eligibility()
    {
        var driver = NewDriver();
        driver.AddCertificate(
            Guid.CreateVersion7(), CertificateKind.ADR, "ADR-1", Today.AddYears(-1), Today.AddYears(4), null, Now);

        Assert.False(driver.CanDriveOn(Today));
        Assert.Single(driver.Certificates);
    }
}

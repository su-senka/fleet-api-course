using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Seeding;

namespace Fleet.Modules.Drivers.Tests;

/// <summary>
/// The Drivers seed data has to contain the awkward cases on purpose, or nothing a student writes
/// against it will ever exercise them.
/// </summary>
public sealed class DriversSeedDataTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Anchor.UtcDateTime);

    [Fact]
    public void It_produces_sixty_drivers_and_the_certificate_volume_the_brief_asks_for()
    {
        var drivers = DriversSeedData.BuildDrivers(Anchor);
        var certificates = drivers.SelectMany(driver => driver.Certificates).Count();

        Assert.Equal(60, drivers.Count);

        // An exact number, not a range. The seed is supposed to be reproducible, so if this
        // changes it is because somebody changed the generator, and that is worth noticing.
        Assert.Equal(177, certificates);
    }

    [Fact]
    public void It_produces_the_same_rows_every_time()
    {
        var first = DriversSeedData.BuildDrivers(Anchor);
        var second = DriversSeedData.BuildDrivers(Anchor);

        Assert.Equal(
            first.Select(driver => (driver.Id, driver.EmployeeNumber, driver.Name)),
            second.Select(driver => (driver.Id, driver.EmployeeNumber, driver.Name)));
    }

    [Fact]
    public void Employee_numbers_are_unique()
    {
        var drivers = DriversSeedData.BuildDrivers(Anchor);

        Assert.Equal(
            drivers.Count,
            drivers.Select(driver => driver.EmployeeNumber).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_three_keycloak_drivers_are_present_and_can_sign_in()
    {
        // These line up with realm-export.json. Sign in as driver.dvorak and this is your row -
        // which is what makes "a driver reads only their own bookings" demonstrable at all.
        var drivers = DriversSeedData.BuildDrivers(Anchor);

        var withLogins = drivers.Where(driver => driver.UserId is not null).ToList();

        Assert.Equal(3, withLogins.Count);
        Assert.Equal(
            ["driver.cerna", "driver.dvorak", "driver.prochazka"],
            withLogins.Select(driver => driver.UserId).Order(StringComparer.Ordinal));

        var dvorak = drivers.Single(driver => driver.UserId == "driver.dvorak");
        Assert.Equal("EMP-1004", dvorak.EmployeeNumber);
    }

    [Fact]
    public void Some_drivers_have_no_licence_at_all()
    {
        var drivers = DriversSeedData.BuildDrivers(Anchor);

        var withoutLicence = drivers
            .Where(driver => driver.Certificates.All(c => c.Kind != CertificateKind.Licence))
            .ToList();

        Assert.Equal(4, withoutLicence.Count);
        Assert.All(withoutLicence, driver => Assert.False(driver.CanDriveOn(Today)));
    }

    [Fact]
    public void Some_drivers_have_an_expired_licence()
    {
        var drivers = DriversSeedData.BuildDrivers(Anchor);

        var expired = drivers
            .Where(driver =>
                driver.Certificates.Any(c => c.Kind == CertificateKind.Licence && c.IsCurrent)
                && !driver.CanDriveOn(Today))
            .ToList();

        Assert.Equal(5, expired.Count);
    }

    [Fact]
    public void Some_licences_expire_within_the_next_thirty_days()
    {
        // The window the CertificateExpiringSoon scan uses. If the seed has nothing in it, the
        // week-N assignment about that event has nothing to show.
        var drivers = DriversSeedData.BuildDrivers(Anchor);

        var expiringSoon = drivers
            .SelectMany(driver => driver.Certificates)
            .Where(certificate =>
                certificate.Kind == CertificateKind.Licence
                && certificate.IsCurrent
                && certificate.ExpiresOn >= Today
                && certificate.ExpiresOn <= Today.AddDays(30))
            .ToList();

        Assert.Equal(6, expiringSoon.Count);
    }

    [Fact]
    public void Renewed_licences_leave_exactly_one_current_certificate_behind()
    {
        // Guards the ordering bug where the historical licence is added last and quietly becomes
        // the current one, retiring a quarter of the fleet's drivers.
        var drivers = DriversSeedData.BuildDrivers(Anchor);

        var renewed = drivers
            .Where(driver => driver.Certificates.Count(c => c.Kind == CertificateKind.Licence) > 1)
            .ToList();

        Assert.NotEmpty(renewed);

        foreach (var driver in renewed)
        {
            var licences = driver.Certificates.Where(c => c.Kind == CertificateKind.Licence).ToList();

            Assert.Single(licences, c => c.IsCurrent);

            // The surviving one must be the newer of the two, not the one it replaced.
            var current = licences.Single(c => c.IsCurrent);
            var superseded = licences.Where(c => !c.IsCurrent);
            Assert.All(superseded, old => Assert.True(old.ExpiresOn < current.ExpiresOn));
        }
    }

    [Fact]
    public void Most_drivers_can_actually_drive()
    {
        var drivers = DriversSeedData.BuildDrivers(Anchor);

        var eligible = drivers.Count(driver => driver.CanDriveOn(Today));

        Assert.InRange(eligible, 45, 56);
    }

    [Fact]
    public void Some_certificates_have_a_scan_and_some_do_not()
    {
        var certificates = DriversSeedData.BuildDrivers(Anchor)
            .SelectMany(driver => driver.Certificates)
            .ToList();

        Assert.Contains(certificates, certificate => certificate.ScanBlobId is not null);
        Assert.Contains(certificates, certificate => certificate.ScanBlobId is null);
    }
}

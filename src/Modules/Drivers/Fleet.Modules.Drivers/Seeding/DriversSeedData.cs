using Fleet.Common.Seeding;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Domain;

namespace Fleet.Modules.Drivers.Seeding;

/// <summary>
/// Builds the Drivers seed data in memory. Pure: same input, same rows, every time.
/// </summary>
/// <remarks>
/// The certificate dates are generated relative to an anchor date rather than hard-coded, so that
/// "expires in nine days" is still true next year. Everything else - ids, names, employee numbers,
/// who holds what - is fixed.
/// </remarks>
internal static class DriversSeedData
{
    public const int DriverCount = 60;
    public const int RandomSeed = 20260101;

    /// <summary>
    /// The three drivers that exist in the Keycloak realm, placed at fixed indices so their
    /// employee numbers line up with the <c>employee_number</c> claim in <c>realm-export.json</c>.
    /// Sign in as any of them and you are a real row in this table.
    /// </summary>
    private static readonly Dictionary<int, (string Name, string UserId)> KeycloakDrivers = new()
    {
        [3] = ("Martin Dvorak", "driver.dvorak"),
        [4] = ("Lucie Cerna", "driver.cerna"),
        [5] = ("Jakub Prochazka", "driver.prochazka"),
    };

    private static readonly string[] GivenNames =
    [
        "Petr", "Jana", "Martin", "Tomas", "Jakub", "Eva", "Pavel", "Michal", "Jiri", "David",
        "Lucie", "Hana", "Katerina", "Marie", "Lenka", "Veronika", "Tereza", "Josef", "Milan", "Zdenek",
    ];

    private static readonly string[] FamilyNames =
    [
        "Novak", "Svoboda", "Dvorak", "Cerny", "Prochazka", "Kucera", "Vesely", "Horak", "Nemec", "Marek",
        "Pospisil", "Pokorny", "Hajek", "Kral", "Jelinek", "Ruzicka", "Benes", "Fiala", "Sedlacek", "Dolezal",
        "Zeman", "Kolar", "Navratil", "Cermak", "Vanek", "Urban", "Blazek", "Kriz", "Kovar", "Bartos",
    ];

    /// <summary>Drivers who hold no licence at all. They must not be bookable.</summary>
    private static readonly int[] DriversWithoutLicence = [17, 34, 41, 58];

    /// <summary>Drivers whose licence expired some time ago. They must not be bookable either.</summary>
    private static readonly int[] DriversWithExpiredLicence = [7, 22, 29, 46, 51];

    /// <summary>
    /// Drivers whose licence expires within the next 30 days. These are what the week's expiry
    /// scan is supposed to find, so there needs to be a known number of them.
    /// </summary>
    private static readonly int[] DriversWithLicenceExpiringSoon = [2, 13, 26, 38, 44, 55];

    /// <summary>
    /// Builds every driver with their certificates.
    /// </summary>
    /// <param name="anchor">"Today" for the purposes of seeding. Expiry dates are relative to it.</param>
    public static IReadOnlyList<Driver> BuildDrivers(DateTimeOffset anchor)
    {
        var random = new Random(RandomSeed);
        var today = DateOnly.FromDateTime(anchor.UtcDateTime);
        var drivers = new List<Driver>(DriverCount);

        for (var index = 0; index < DriverCount; index++)
        {
            var employeeNumber = $"EMP-{1001 + index:0000}";

            var (name, userId) = KeycloakDrivers.TryGetValue(index, out var known)
                ? known
                : ($"{GivenNames[index % GivenNames.Length]} {FamilyNames[index % FamilyNames.Length]}", null);

            var created = Driver.Register(
                DeterministicGuid.Create("driver", index),
                employeeNumber,
                name,
                userId);

            if (created.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed data produced an invalid driver at index {index}: {created.Error}");
            }

            var driver = created.Value;
            AddCertificates(driver, index, today, anchor, random);
            drivers.Add(driver);
        }

        return drivers;
    }

    private static void AddCertificates(
        Driver driver,
        int index,
        DateOnly today,
        DateTimeOffset anchor,
        Random random)
    {
        var holdsLicence = !DriversWithoutLicence.Contains(index);

        // The historical licence goes in first. AddCertificate supersedes whatever is current when
        // it runs, so the row added last is the one that counts - add these the other way round
        // and every fourth driver silently ends up with an expired licence as their current one.
        if (holdsLicence && index % 4 == 1)
        {
            var previousExpiry = today.AddDays(-random.Next(200, 900));
            Issue(
                driver,
                index,
                CertificateKind.Licence,
                previousExpiry.AddYears(-10),
                previousExpiry,
                anchor,
                random,
                suffix: "old");
        }

        if (holdsLicence)
        {
            var expiresOn = LicenceExpiry(index, today, random);
            Issue(driver, index, CertificateKind.Licence, expiresOn.AddYears(-10), expiresOn, anchor, random);
        }

        // A superseded medical for every third driver, again added before the current one. Medicals
        // are renewed more often than licences, so most drivers have a paper trail.
        if (index % 3 == 2)
        {
            var previousExpiry = today.AddDays(-random.Next(120, 700));
            Issue(
                driver,
                index,
                CertificateKind.Medical,
                previousExpiry.AddYears(-2),
                previousExpiry,
                anchor,
                random,
                suffix: "old");
        }

        // Four drivers in five have a current medical. The remaining twelve are left holding only
        // a lapsed one, or none - which is the reason a "certificates expiring" report is worth
        // building at all.
        if (index % 5 != 4)
        {
            var expiresOn = today.AddDays(random.Next(-40, 400));
            Issue(driver, index, CertificateKind.Medical, expiresOn.AddYears(-2), expiresOn, anchor, random);
        }

        // ADR is a qualification two drivers in three hold. It never affects eligibility; it is
        // here so that filtering certificates by kind returns something interesting.
        if (index % 3 != 2)
        {
            var expiresOn = today.AddDays(random.Next(20, 900));
            Issue(driver, index, CertificateKind.ADR, expiresOn.AddYears(-5), expiresOn, anchor, random);
        }
    }

    private static DateOnly LicenceExpiry(int index, DateOnly today, Random random)
    {
        if (DriversWithExpiredLicence.Contains(index))
        {
            return today.AddDays(-random.Next(10, 400));
        }

        if (DriversWithLicenceExpiringSoon.Contains(index))
        {
            // Inside the 30-day window, and never today itself, so the boundary cases in the
            // expiry tests stay under the tests' own control rather than the seed's.
            return today.AddDays(random.Next(1, 30));
        }

        return today.AddDays(random.Next(120, 2_500));
    }

    private static void Issue(
        Driver driver,
        int index,
        CertificateKind kind,
        DateOnly issuedOn,
        DateOnly expiresOn,
        DateTimeOffset anchor,
        Random random,
        string suffix = "current")
    {
        var number = $"{kind.ToString().ToUpperInvariant()}-{random.Next(100_000, 999_999)}";

        var issued = driver.AddCertificate(
            DeterministicGuid.Create($"certificate:{index}:{kind}:{suffix}"),
            kind,
            number,
            issuedOn,
            expiresOn,
            // Only some certificates have been scanned. A null blob id is a perfectly ordinary
            // state, and the endpoint that serves the scan has to cope with it.
            scanBlobId: index % 3 == 0 ? $"certificates/{index}-{kind}.pdf".ToLowerInvariant() : null,
            at: anchor);

        if (issued.IsFailure)
        {
            throw new InvalidOperationException(
                $"Seed data produced an invalid {kind} certificate for driver {index}: {issued.Error}");
        }
    }
}

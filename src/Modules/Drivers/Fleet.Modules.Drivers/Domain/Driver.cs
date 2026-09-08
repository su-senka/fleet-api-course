using Fleet.Common.Results;
using Fleet.Modules.Drivers.Contracts;

namespace Fleet.Modules.Drivers.Domain;

/// <summary>
/// Someone who may be assigned to a booking.
/// </summary>
/// <remarks>
/// The rule that matters to the rest of the system is <see cref="CanDriveOn"/>: no current,
/// unexpired licence, no booking. It lives here so that Bookings can ask the question without
/// knowing what a certificate is.
/// </remarks>
internal sealed class Driver
{
    private readonly List<Certificate> _certificates = [];

    private Driver()
    {
        EmployeeNumber = string.Empty;
        Name = string.Empty;
    }

    private Driver(Guid id, string employeeNumber, string name, string? userId)
    {
        Id = id;
        EmployeeNumber = employeeNumber;
        Name = name;
        UserId = userId;
    }

    public const int EmployeeNumberMaxLength = 20;
    public const int NameMaxLength = 120;

    public Guid Id { get; private set; }

    /// <summary>Unique within the company. Upper-cased on the way in.</summary>
    public string EmployeeNumber { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// The identity-provider subject this driver signs in as. Null for drivers with no login -
    /// most of the fleet, in practice.
    /// </summary>
    public string? UserId { get; private set; }

    public IReadOnlyCollection<Certificate> Certificates => _certificates;

    public static Result<Driver> Register(Guid id, string employeeNumber, string name, string? userId)
    {
        var normalisedNumber = employeeNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        var trimmedName = name?.Trim() ?? string.Empty;

        if (normalisedNumber.Length == 0)
        {
            return Error.Validation("driver.employee_number_required", "A driver needs an employee number.");
        }

        if (normalisedNumber.Length > EmployeeNumberMaxLength)
        {
            return Error.Validation(
                "driver.employee_number_too_long",
                $"An employee number is at most {EmployeeNumberMaxLength} characters.");
        }

        if (trimmedName.Length == 0)
        {
            return Error.Validation("driver.name_required", "A driver needs a name.");
        }

        if (trimmedName.Length > NameMaxLength)
        {
            return Error.Validation(
                "driver.name_too_long",
                $"A driver's name is at most {NameMaxLength} characters.");
        }

        return new Driver(id, normalisedNumber, trimmedName, string.IsNullOrWhiteSpace(userId) ? null : userId.Trim());
    }

    /// <summary>
    /// Adds a certificate, superseding any current one of the same kind.
    /// </summary>
    /// <param name="at">
    /// When the supersession happened, from <c>IClock</c>. Passed in rather than read from
    /// <c>DateTimeOffset.UtcNow</c> so that a test can renew a licence "in 2027" without waiting.
    /// </param>
    public Result<Certificate> AddCertificate(
        Guid certificateId,
        CertificateKind kind,
        string number,
        DateOnly issuedOn,
        DateOnly expiresOn,
        string? scanBlobId,
        DateTimeOffset at)
    {
        var issued = Certificate.Issue(certificateId, Id, kind, number, issuedOn, expiresOn, scanBlobId);
        if (issued.IsFailure)
        {
            return issued.Error;
        }

        foreach (var existing in _certificates.Where(c => c.Kind == kind && c.IsCurrent))
        {
            existing.Supersede(at);
        }

        _certificates.Add(issued.Value);
        return issued.Value;
    }

    /// <summary>
    /// Whether this driver may drive on the given day.
    /// </summary>
    /// <remarks>
    /// Only the current licence counts. A superseded one that happens to still be inside its
    /// validity window does not bring a driver back, and neither does a medical - the brief is
    /// specific that it is the licence that blocks a booking.
    /// </remarks>
    public bool CanDriveOn(DateOnly date) =>
        _certificates.Any(certificate =>
            certificate.Kind == CertificateKind.Licence
            && certificate.IsCurrent
            && certificate.IsValidOn(date));
}

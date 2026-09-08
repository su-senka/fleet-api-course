using Fleet.Common.Paging;
using Fleet.Common.Results;
using Fleet.Common.Time;
using Fleet.Modules.Drivers.Contracts;
using Fleet.Modules.Drivers.Domain;
using Fleet.Modules.Drivers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Drivers.Application;

/// <summary>The Drivers application service. Finished, tested, and unaware of HTTP.</summary>
internal sealed class DriverService(DriversDbContext dbContext, IClock clock) : IDriverService
{
    private static readonly string[] SortableFieldNames = ["name", "employeeNumber"];

    public async Task<Result<PagedResult<DriverDto>>> ListAsync(
        PageRequest page,
        SortRequest? sort,
        FilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        if (sort is not null && !SortableFieldNames.Contains(sort.Field, StringComparer.OrdinalIgnoreCase))
        {
            return Error.Validation(
                "driver.sort_field_unknown",
                $"Cannot sort by '{sort.Field}'. Try one of: {string.Join(", ", SortableFieldNames)}.");
        }

        var today = clock.Today;
        var query = dbContext.Drivers.AsNoTracking();

        if (filter.Search is { } search)
        {
            // ILike is Postgres' case-insensitive LIKE. It is exposed through EF.Functions rather
            // than by calling ToLower() on both sides, which would defeat any index on the column.
            var pattern = $"%{search}%";
            query = query.Where(driver =>
                EF.Functions.ILike(driver.Name, pattern)
                || EF.Functions.ILike(driver.EmployeeNumber, pattern));
        }

        // Project before filtering, so "holds a current licence valid today" is written once and
        // then reused by the filter below. Postgres sees one EXISTS subquery either way; we just
        // avoid stating the rule twice in C#.
        var projected = query.Select(driver => new
        {
            driver.Id,
            driver.EmployeeNumber,
            driver.Name,
            driver.UserId,
            HasValidLicence = driver.Certificates.Any(certificate =>
                certificate.Kind == CertificateKind.Licence
                && certificate.SupersededAt == null
                && certificate.IssuedOn <= today
                && certificate.ExpiresOn >= today),
        });

        if (bool.TryParse(filter["hasValidLicence"], out var hasValidLicence))
        {
            projected = projected.Where(row => row.HasValidLicence == hasValidLicence);
        }

        var totalCount = await projected.LongCountAsync(cancellationToken);

        // Employee number is unique, so it is both the default order and the tie-breaker that
        // keeps paging stable when two drivers share a name.
        var descending = sort?.Direction == SortDirection.Descending;
        var ordered = (sort?.Field.ToLowerInvariant(), descending) switch
        {
            ("name", false) => projected.OrderBy(row => row.Name).ThenBy(row => row.EmployeeNumber),
            ("name", true) => projected.OrderByDescending(row => row.Name).ThenBy(row => row.EmployeeNumber),
            (_, true) => projected.OrderByDescending(row => row.EmployeeNumber),
            _ => projected.OrderBy(row => row.EmployeeNumber),
        };

        var rows = await ordered
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        IReadOnlyList<DriverDto> items =
        [
            .. rows.Select(row => new DriverDto(row.Id, row.EmployeeNumber, row.Name, row.UserId, row.HasValidLicence))
        ];

        return new PagedResult<DriverDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<DriverDetailDto>> GetAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .Include(d => d.Certificates)
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        return driver is null ? NotFound(driverId) : ToDetail(driver, clock.Today);
    }

    public async Task<Result<DriverDetailDto>> RegisterAsync(
        RegisterDriverCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = Driver.Register(Guid.CreateVersion7(), command.EmployeeNumber, command.Name, command.UserId);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var driver = created.Value;

        var taken = await dbContext.Drivers
            .AnyAsync(d => d.EmployeeNumber == driver.EmployeeNumber, cancellationToken);

        if (taken)
        {
            return Error.Conflict(
                "driver.employee_number_taken",
                $"Employee number {driver.EmployeeNumber} already belongs to another driver.");
        }

        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDetail(driver, clock.Today);
    }

    public async Task<Result<CertificateDto>> AddCertificateAsync(
        Guid driverId,
        AddCertificateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var driver = await dbContext.Drivers
            .Include(d => d.Certificates)
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return NotFound(driverId);
        }

        var added = driver.AddCertificate(
            Guid.CreateVersion7(),
            command.Kind,
            command.Number,
            command.IssuedOn,
            command.ExpiresOn,
            command.ScanBlobId,
            clock.UtcNow);

        if (added.IsFailure)
        {
            return added.Error;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(added.Value, clock.Today);
    }

    public async Task<Result<IReadOnlyList<CertificateDto>>> ListCertificatesAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);
        if (!exists)
        {
            return NotFound(driverId);
        }

        var today = clock.Today;

        var certificates = await dbContext.Certificates
            .AsNoTracking()
            .Where(certificate => certificate.DriverId == driverId)
            .OrderByDescending(certificate => certificate.ExpiresOn)
            .Select(certificate => new CertificateDto(
                certificate.Id,
                certificate.DriverId,
                certificate.Kind,
                certificate.Number,
                certificate.IssuedOn,
                certificate.ExpiresOn,
                certificate.ScanBlobId,
                certificate.ExpiresOn < today))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CertificateDto>>.Success(certificates);
    }

    public async Task<Result<IReadOnlyList<CertificateExpiringSoon>>> FindExpiringCertificatesAsync(
        DateOnly on,
        int withinDays,
        CancellationToken cancellationToken = default)
    {
        if (withinDays < 0)
        {
            return Error.Validation(
                "certificate.window_negative",
                "The expiry window cannot be negative.");
        }

        var horizon = on.AddDays(withinDays);

        // Inclusive at both ends: a certificate expiring today is expiring soon, and so is one
        // expiring on the last day of the window. Already-expired certificates are excluded -
        // they are a different problem, and re-announcing them every day would be noise.
        var expiring = await dbContext.Certificates
            .AsNoTracking()
            .Where(certificate =>
                certificate.SupersededAt == null
                && certificate.ExpiresOn >= on
                && certificate.ExpiresOn <= horizon)
            .Join(
                dbContext.Drivers.AsNoTracking(),
                certificate => certificate.DriverId,
                driver => driver.Id,
                (certificate, driver) => new { certificate, driver })
            .OrderBy(row => row.certificate.ExpiresOn)
            .ThenBy(row => row.driver.EmployeeNumber)
            .Select(row => new CertificateExpiringSoon(
                row.certificate.Id,
                row.driver.Id,
                row.driver.Name,
                row.driver.EmployeeNumber,
                row.certificate.Kind,
                row.certificate.ExpiresOn,
                row.certificate.ExpiresOn.DayNumber - on.DayNumber))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CertificateExpiringSoon>>.Success(expiring);
    }

    private static Error NotFound(Guid driverId) =>
        Error.NotFound("driver.not_found", $"There is no driver with id {driverId}.");

    private static CertificateDto ToDto(Certificate certificate, DateOnly today) =>
        new(certificate.Id,
            certificate.DriverId,
            certificate.Kind,
            certificate.Number,
            certificate.IssuedOn,
            certificate.ExpiresOn,
            certificate.ScanBlobId,
            certificate.ExpiresOn < today);

    private static DriverDetailDto ToDetail(Driver driver, DateOnly today) =>
        new(driver.Id,
            driver.EmployeeNumber,
            driver.Name,
            driver.UserId,
            driver.CanDriveOn(today),
            [.. driver.Certificates
                .OrderByDescending(certificate => certificate.ExpiresOn)
                .Select(certificate => ToDto(certificate, today))]);
}

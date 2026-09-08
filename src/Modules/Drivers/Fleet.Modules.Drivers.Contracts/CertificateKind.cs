namespace Fleet.Modules.Drivers.Contracts;

/// <summary>The kinds of certificate a driver can hold.</summary>
public enum CertificateKind
{
    /// <summary>Driving licence. Missing or expired, and the driver cannot be booked at all.</summary>
    Licence = 1,

    /// <summary>Medical fitness check. Tracked and warned about, but does not block a booking.</summary>
    Medical = 2,

    /// <summary>Dangerous goods certification. Needed for some loads, not for driving as such.</summary>
    ADR = 3,
}

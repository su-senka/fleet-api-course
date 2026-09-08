using Fleet.Common.Messaging;

namespace Fleet.Modules.Drivers.Contracts;

/// <summary>
/// Announced once for each certificate that is within 30 days of expiring.
/// </summary>
/// <remarks>
/// <para>
/// Past tense, and a statement of fact: the Drivers module says a certificate is about to expire.
/// It does not say "send an email", because it does not know or care that Notifications exists.
/// Notifications subscribes and decides what to do; if nobody subscribed, nothing would break.
/// </para>
/// <para>
/// It lives in <c>.Contracts</c> because a subscriber has to be able to deserialise it. That makes
/// every property here part of the module's public API - changing one is a breaking change for
/// every module that handles the event.
/// </para>
/// </remarks>
public sealed record CertificateExpiringSoon(
    Guid CertificateId,
    Guid DriverId,
    string DriverName,
    string EmployeeNumber,
    CertificateKind Kind,
    DateOnly ExpiresOn,
    int DaysRemaining) : IntegrationEvent;

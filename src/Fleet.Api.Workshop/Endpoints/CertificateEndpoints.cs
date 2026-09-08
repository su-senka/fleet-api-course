namespace Fleet.Api.Workshop.Endpoints;

/// <summary>
/// Certificates: a sub-resource, and the first thing that is not JSON.
/// </summary>
/// <remarks>
/// <para>
/// The module contract is <c>IDriverService.AddCertificateAsync</c> and
/// <c>ListCertificatesAsync</c>. A certificate only exists inside a driver, which is the argument
/// for <c>/drivers/{driverId}/certificates</c> rather than a top-level <c>/certificates</c> - but
/// it is an argument, not a law, and it is worth being able to defend either.
/// </para>
/// <para>
/// Certificates carry a scanned document, held in the blob store as a <c>ScanBlobId</c>. Serving
/// it raises questions JSON never does: what content type, what filename, does the browser display
/// it or download it, and what happens when the id is real but the file is gone. <c>IBlobStore</c>
/// in <c>Fleet.Common.Storage</c> is finished and injected; the endpoint is not.
/// </para>
/// </remarks>
internal static class CertificateEndpoints
{
    // TODO(week-6): map GET /drivers/{driverId}/certificates and POST to the same path.
    // TODO(week-6): serve the scan - GET /drivers/{driverId}/certificates/{certificateId}/scan.
    //
    // See docs/assignments/week-6.md.
    // Note that a driver with no certificates and a driver who does not exist are different
    // answers: one is an empty list, the other is a 404.
}

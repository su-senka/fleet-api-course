using Fleet.Api.Workshop.Http;
using Fleet.Api.Workshop.Requests;
using Fleet.Common.Storage;
using Fleet.Modules.Drivers.Contracts;

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
    public static void MapCertificateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/drivers/{driverId}/certificates").WithTags("Certificates");

        group.MapGet("/", ListCertificatesAsync)
            .WithName("ListDriverCertificates")
            .WithSummary("List all certificates of a driver")
            .Produces<IReadOnlyList<CertificateDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", AddCertificateAsync)
            .WithName("AddCertificate")
            .WithSummary("Add a certificate to a driver")
            .WithValidation<AddCertificateRequest>()
            .Produces<CertificateDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{certificateId:guid}/scan", GetScanAsync)
            .WithName("GetCertificateScan")
            .WithSummary("Download a certificate's scanned document")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListCertificatesAsync(Guid driverId, IDriverService drivers, HttpContext http)
    {
        var result = await drivers.ListCertificatesAsync(driverId, http.RequestAborted);

        return result.Match(http, Results.Ok);
    }

    private static async Task<IResult> AddCertificateAsync(
        Guid driverId,
        AddCertificateRequest request,
        IDriverService drivers,
        HttpContext http)
    {
        var command = new AddCertificateCommand(
            request.Kind, request.Number, request.IssuedOn, request.ExpiresOn, request.ScanBlobId);

        var result = await drivers.AddCertificateAsync(driverId, command, http.RequestAborted);

        return result.Match(http, certificate => Results.Created(
            $"/drivers/{driverId}/certificates/{certificate.Id}", certificate));
    }

    private static async Task<IResult> GetScanAsync(
        Guid driverId,
        Guid certificateId,
        IDriverService drivers,
        IBlobStore blobs,
        HttpContext http)
    {
        var certificates = await drivers.ListCertificatesAsync(driverId, http.RequestAborted);
        if (certificates.IsFailure)
        {
            return ProblemResults.From(certificates.Error, http);
        }

        var certificate = certificates.Value.FirstOrDefault(c => c.Id == certificateId);
        if (certificate?.ScanBlobId is null)
        {
            return Results.NotFound();
        }

        var blob = await blobs.GetAsync(certificate.ScanBlobId, http.RequestAborted);
        if (blob is null)
        {
            return Results.NotFound();
        }

        return Results.Stream(blob.Content, blob.ContentType, blob.FileName);
    }
}

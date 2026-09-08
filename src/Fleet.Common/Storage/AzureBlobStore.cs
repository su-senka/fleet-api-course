using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Fleet.Common.Storage;

/// <summary>
/// <see cref="IBlobStore"/> over Azure Blob Storage, which locally means Azurite in Compose.
/// </summary>
/// <remarks>
/// <para>
/// The one place in the repository that mentions Azure. Modules see <see cref="IBlobStore"/> and
/// nothing else, so swapping this for S3, or for a directory on disk, touches this file only.
/// </para>
/// <para>
/// A blob id is <c>container/name</c>. Not a URL: handing a client a storage URL means either
/// making the container public or minting a signed one, and both are decisions the API layer
/// should make deliberately rather than inherit from a DTO.
/// </para>
/// </remarks>
internal sealed class AzureBlobStore(
    BlobServiceClient serviceClient,
    ILogger<AzureBlobStore> logger) : IBlobStore
{
    private const string FileNameMetadataKey = "fileName";

    public async Task<string> SaveAsync(
        string container,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var containerClient = serviceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // The name is made unique here rather than trusting the caller: two drivers uploading
        // "licence.pdf" must not overwrite each other.
        var blobName = $"{Guid.CreateVersion7():N}-{SanitiseFileName(fileName)}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },

                // The original name is kept as metadata so a download can offer it back in a
                // Content-Disposition header, without it having to survive in the blob id.
                Metadata = new Dictionary<string, string> { [FileNameMetadataKey] = fileName },
            },
            cancellationToken);

        var blobId = $"{container}/{blobName}";

        logger.LogInformation("Stored blob {BlobId} ({ContentType})", blobId, contentType);

        return blobId;
    }

    public async Task<BlobContent?> GetAsync(string blobId, CancellationToken cancellationToken = default)
    {
        if (!TrySplit(blobId, out var container, out var blobName))
        {
            return null;
        }

        var blobClient = serviceClient.GetBlobContainerClient(container).GetBlobClient(blobName);

        try
        {
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            var details = response.Value.Details;

            return new BlobContent(
                response.Value.Content,
                details.ContentType ?? "application/octet-stream",
                OriginalFileName(details.Metadata, blobName),
                details.ContentLength);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            // A missing blob is an ordinary answer to "is this here?", not a fault.
            return null;
        }
    }

    public async Task<BlobInfo?> GetInfoAsync(string blobId, CancellationToken cancellationToken = default)
    {
        if (!TrySplit(blobId, out var container, out var blobName))
        {
            return null;
        }

        var blobClient = serviceClient.GetBlobContainerClient(container).GetBlobClient(blobName);

        try
        {
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            return new BlobInfo(
                blobId,
                properties.Value.ContentType ?? "application/octet-stream",
                OriginalFileName(properties.Value.Metadata, blobName),
                properties.Value.ContentLength,
                properties.Value.CreatedOn);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string blobId, CancellationToken cancellationToken = default)
    {
        if (!TrySplit(blobId, out var container, out var blobName))
        {
            return false;
        }

        var blobClient = serviceClient.GetBlobContainerClient(container).GetBlobClient(blobName);

        var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        return response.Value;
    }

    private static string OriginalFileName(IDictionary<string, string>? metadata, string fallback) =>
        metadata is not null && metadata.TryGetValue(FileNameMetadataKey, out var name) ? name : fallback;

    /// <summary>Splits <c>container/name</c>. Returns false for anything that is not one.</summary>
    private static bool TrySplit(string? blobId, out string container, out string blobName)
    {
        container = string.Empty;
        blobName = string.Empty;

        if (string.IsNullOrWhiteSpace(blobId))
        {
            return false;
        }

        var separator = blobId.IndexOf('/', StringComparison.Ordinal);

        if (separator <= 0 || separator == blobId.Length - 1)
        {
            return false;
        }

        container = blobId[..separator];
        blobName = blobId[(separator + 1)..];

        return true;
    }

    private static string SanitiseFileName(string fileName)
    {
        var trimmed = Path.GetFileName(fileName.Trim());
        var cleaned = new string([.. trimmed.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-')]);

        return cleaned.Length == 0
            ? "file"
            : cleaned.ToLower(CultureInfo.InvariantCulture);
    }
}

namespace Fleet.Common.Storage;

/// <summary>A stored file, returned by <see cref="IBlobStore.GetAsync"/>.</summary>
/// <remarks>The caller owns <see cref="Content"/> and must dispose it.</remarks>
public sealed record BlobContent(Stream Content, string ContentType, string FileName, long Length)
    : IDisposable
{
    public void Dispose() => Content.Dispose();
}

/// <summary>Metadata about a stored file, without its bytes.</summary>
public sealed record BlobInfo(string BlobId, string ContentType, string FileName, long Length, DateTimeOffset CreatedAt);

/// <summary>
/// Somewhere to put bytes that do not belong in Postgres: certificate scans, generated reports.
/// </summary>
/// <remarks>
/// Backed by Azurite in local development, which speaks the Azure Blob Storage protocol. Modules
/// see only this interface, so nothing in <c>src/Modules</c> mentions Azure. Note that the
/// interface deals in streams and never in <c>byte[]</c> - a 40 MB report should not have to
/// exist in memory twice.
/// </remarks>
public interface IBlobStore
{
    /// <summary>Stores content and returns the blob id needed to read it back.</summary>
    Task<string> SaveAsync(
        string container,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a blob, or returns <c>null</c> when the id is unknown.</summary>
    Task<BlobContent?> GetAsync(string blobId, CancellationToken cancellationToken = default);

    /// <summary>Reads a blob's metadata without transferring its bytes.</summary>
    Task<BlobInfo?> GetInfoAsync(string blobId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a blob. Returns <c>false</c> when it was already gone.</summary>
    Task<bool> DeleteAsync(string blobId, CancellationToken cancellationToken = default);
}

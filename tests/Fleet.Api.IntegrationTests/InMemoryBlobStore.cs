using System.Collections.Concurrent;
using Fleet.Common.Storage;

namespace Fleet.Api.IntegrationTests;

/// <summary>
/// An <see cref="IBlobStore"/> in a dictionary, so the tests need no Azurite.
/// </summary>
/// <remarks>
/// The reference slice does not serve blobs - certificate scans and report CSVs belong to modules
/// with no HTTP surface yet - but Reporting depends on the interface, so the container needs
/// something to resolve. This is the smallest honest something.
/// </remarks>
internal sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType, string FileName)> _blobs = new();

    public Task<string> SaveAsync(
        string container,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);

        var blobId = $"{container}/{Guid.CreateVersion7():N}-{fileName}";
        _blobs[blobId] = (buffer.ToArray(), contentType, fileName);

        return Task.FromResult(blobId);
    }

    public Task<BlobContent?> GetAsync(string blobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.TryGetValue(blobId, out var blob)
            ? new BlobContent(new MemoryStream(blob.Content), blob.ContentType, blob.FileName, blob.Content.Length)
            : null);

    public Task<BlobInfo?> GetInfoAsync(string blobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.TryGetValue(blobId, out var blob)
            ? new BlobInfo(blobId, blob.ContentType, blob.FileName, blob.Content.Length, DateTimeOffset.UtcNow)
            : null);

    public Task<bool> DeleteAsync(string blobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.TryRemove(blobId, out _));
}

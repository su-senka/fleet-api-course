using System.Collections.Concurrent;
using Fleet.Common.Storage;

namespace Fleet.Modules.Bookings.Tests;

/// <summary>
/// An <see cref="IBlobStore"/> in a dictionary, so seeding drivers' certificate scans needs no
/// Azurite here - these tests are about double-booking, not blob storage.
/// </summary>
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

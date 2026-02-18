namespace Dataisland.MinIO;

public interface IFileStorage
{
    Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default);
    Task UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string path, CancellationToken ct = default);
    Task<bool> ExistsAsync(string bucket, string path, CancellationToken ct = default);
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string path, int expiryInSeconds = 604800, CancellationToken ct = default);
}

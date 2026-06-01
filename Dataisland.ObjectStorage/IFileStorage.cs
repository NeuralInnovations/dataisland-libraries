namespace Dataisland.ObjectStorage;

public interface IFileStorage
{
    Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default);
    Task MoveAsync(string bucket, string sourcePath, string destinationPath, CancellationToken ct = default);
    Task UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default);
    Task<int> CopyPrefixAsync(string bucket, string sourcePrefix, string targetPrefix, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string path, CancellationToken ct = default);
    Task<int> DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default);
    Task<bool> ExistsAsync(string bucket, string path, CancellationToken ct = default);
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string path, int expiryInSeconds = 604800, CancellationToken ct = default);
}

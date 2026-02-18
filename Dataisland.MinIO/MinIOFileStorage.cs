using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Dataisland.MinIO;

public class MinIOFileStorage : IFileStorage
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinIOFileStorage> _logger;

    public MinIOFileStorage(MinIOOptions options, ILogger<MinIOFileStorage> logger)
    {
        _logger = logger;

        var builder = new MinioClient()
            .WithEndpoint(options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey);

        if (options.UseSsl)
            builder = builder.WithSSL();

        _client = builder.Build();
    }

    public async Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithCallbackStream(stream => stream.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType), ct);
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(path), ct);
    }

    public async Task<bool> ExistsAsync(string bucket, string path, CancellationToken ct = default)
    {
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(path), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
            _logger.LogInformation("Created bucket {Bucket}", bucket);
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string path, int expiryInSeconds = 604800, CancellationToken ct = default)
    {
        return await _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithExpiry(expiryInSeconds));
    }
}

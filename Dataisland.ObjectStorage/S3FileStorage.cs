using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace Dataisland.ObjectStorage;

public class S3FileStorage : IFileStorage, IAsyncDisposable
{
    private readonly AmazonS3Client _client;
    private readonly ILogger<S3FileStorage> _logger;

    public S3FileStorage(ObjectStorageOptions options, ILogger<S3FileStorage> logger)
    {
        _logger = logger;

        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);

        var config = new AmazonS3Config
        {
            ServiceURL = $"{(options.UseSsl ? "https" : "http")}://{options.Endpoint}",
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            Timeout = TimeSpan.FromSeconds(30),
            RetryMode = RequestRetryMode.Standard,
            MaxErrorRetry = 3
        };

        _client = new AmazonS3Client(credentials, config);
    }

    public async Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = path
        }, ct);

        var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);

        // Always buffer to MemoryStream for reliable upload:
        // 1. Guarantees Content-Length is set (no chunked transfer)
        // 2. UseChunkEncoding=false prevents AWS SDK chunked payload signing
        //    which SeaweedFS stores as chunk manifests instead of actual content
        MemoryStream ms;
        if (content is MemoryStream existing && existing.Position == 0)
        {
            ms = existing;
        }
        else
        {
            ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            ms.Position = 0;
        }

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = path,
            InputStream = ms,
            ContentType = contentType,
            UseChunkEncoding = false,
            Headers = { ContentLength = ms.Length }
        }, ct);
    }

    public async Task<int> CopyPrefixAsync(string bucket, string sourcePrefix, string targetPrefix, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);

        sourcePrefix = NormalizePrefix(sourcePrefix);
        targetPrefix = NormalizePrefix(targetPrefix);
        var copied = 0;
        string? token = null;

        do
        {
            var list = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = sourcePrefix,
                ContinuationToken = token
            }, ct);

            foreach (var obj in list.S3Objects)
            {
                var suffix = obj.Key[sourcePrefix.Length..];
                await _client.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = bucket,
                    SourceKey = obj.Key,
                    DestinationBucket = bucket,
                    DestinationKey = targetPrefix + suffix
                }, ct);
                copied++;
            }

            token = list.IsTruncated ? list.NextContinuationToken : null;
        } while (token is not null);

        return copied;
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucket,
            Key = path
        }, ct);
    }

    public async Task<int> DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        prefix = NormalizePrefix(prefix);
        var deleted = 0;
        string? token = null;

        do
        {
            var list = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix,
                ContinuationToken = token
            }, ct);

            var batch = list.S3Objects
                .Select(x => new KeyVersion { Key = x.Key })
                .ToList();

            if (batch.Count > 0)
            {
                await _client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = bucket,
                    Objects = batch
                }, ct);
                deleted += batch.Count;
            }

            token = list.IsTruncated ? list.NextContinuationToken : null;
        } while (token is not null);

        return deleted;
    }

    public async Task<bool> ExistsAsync(string bucket, string path, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = path
            }, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        try
        {
            await _client.GetBucketLocationAsync(new GetBucketLocationRequest
            {
                BucketName = bucket
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await _client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucket
            }, ct);
            _logger.LogInformation("Created bucket {Bucket}", bucket);
        }
    }

    public Task<string> GetPresignedUrlAsync(string bucket, string path, int expiryInSeconds = 604800, CancellationToken ct = default)
    {
        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = path,
            Expires = DateTime.UtcNow.AddSeconds(expiryInSeconds),
            Verb = HttpVerb.GET
        });

        return Task.FromResult(url);
    }

    private static string NormalizePrefix(string prefix)
    {
        prefix = prefix.TrimStart('/');
        return prefix.EndsWith('/') ? prefix : $"{prefix}/";
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}

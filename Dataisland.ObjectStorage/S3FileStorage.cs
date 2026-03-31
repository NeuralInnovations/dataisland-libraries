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

        // Set ContentLength explicitly to avoid chunked transfer encoding,
        // which causes SeaweedFS to store chunk manifests instead of actual content
        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = path,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true
        };

        if (content.CanSeek)
            putRequest.Headers.ContentLength = content.Length - content.Position;

        await _client.PutObjectAsync(putRequest, ct);
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucket,
            Key = path
        }, ct);
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

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}

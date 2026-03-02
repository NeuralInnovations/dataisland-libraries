using Microsoft.Extensions.Configuration;

namespace Dataisland.ObjectStorage;

public class ObjectStorageOptions
{
    public required string Endpoint { get; set; }
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public bool UseSsl { get; set; } = false;
}

public static class ObjectStorageOptionsExtensions
{
    public static ObjectStorageOptions GetObjectStorageOptions(
        this IConfiguration configuration, string section = "ObjectStorage")
    {
        return configuration.GetSection(section).Get<ObjectStorageOptions>()
               ?? configuration.GetSection("MinIO").Get<ObjectStorageOptions>()!;
    }
}

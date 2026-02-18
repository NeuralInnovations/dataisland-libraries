using Microsoft.Extensions.Configuration;

namespace Dataisland.MinIO;

public class MinIOOptions
{
    public required string Endpoint { get; set; }
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public bool UseSsl { get; set; } = false;
}

public static class MinIOOptionsExtensions
{
    public static MinIOOptions GetMinIOOptions(
        this IConfiguration configuration, string section = "MinIO")
    {
        return configuration.GetSection(section).Get<MinIOOptions>()!;
    }
}

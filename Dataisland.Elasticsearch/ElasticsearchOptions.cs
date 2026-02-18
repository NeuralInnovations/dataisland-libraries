using Microsoft.Extensions.Configuration;

namespace Dataisland.Elasticsearch;

public class ElasticsearchOptions
{
    public required string Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int DefaultVectorDimensions { get; set; } = 1024;
}

public static class ElasticsearchOptionsExtensions
{
    public static ElasticsearchOptions GetElasticsearchOptions(
        this IConfiguration configuration, string section = "Elasticsearch")
    {
        return configuration.GetSection(section).Get<ElasticsearchOptions>()!;
    }
}

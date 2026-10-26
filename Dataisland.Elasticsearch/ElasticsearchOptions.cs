using Microsoft.Extensions.Configuration;

namespace Dataisland.Elasticsearch;

public class ElasticsearchOptions
{
    public required string Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? CertificateFingerprint { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int PingTimeoutSeconds { get; set; } = 10;
    public int DefaultVectorDimensions { get; set; } = 1024;

    /// <summary>
    /// Elasticsearch:StableSearchPreference — the same search request is always answered by the same shard
    /// copies, so it gets the same scores (see <see cref="Dataisland.Elasticsearch.StableSearchPreference"/>). Off by
    /// default: every request then picks a copy anew, as before. Only matters where indices have replicas.
    /// </summary>
    public bool StableSearchPreference { get; set; }
}

public static class ElasticsearchOptionsExtensions
{
    public static ElasticsearchOptions GetElasticsearchOptions(
        this IConfiguration configuration, string section = "Elasticsearch")
    {
        return configuration.GetSection(section).Get<ElasticsearchOptions>()!;
    }
}

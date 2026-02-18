using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.Elasticsearch;

public static class ElasticsearchExtensions
{
    public static IServiceCollection AddElasticsearch(this IServiceCollection services, ElasticsearchOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IElasticClient, ElasticClientImpl>();
        return services;
    }
}

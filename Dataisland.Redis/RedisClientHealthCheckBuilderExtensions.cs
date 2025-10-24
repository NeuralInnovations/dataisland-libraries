using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Dataisland.Redis;

public static class RedisClientHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddRedis(this IHealthChecksBuilder builder)
    {
        return builder.AddRedis(s => s.GetRequiredService<IConnectionMultiplexer>());
    }
}
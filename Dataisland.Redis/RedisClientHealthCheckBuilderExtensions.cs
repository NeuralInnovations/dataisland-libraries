using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Dataisland.Redis;

public static class RedisClientHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddRedis(this IHealthChecksBuilder builder)
    {
        // Tagged readiness — see the Mongo check for why a dependency must not fail liveness.
        return builder.AddRedis(
            s => s.GetRequiredService<IRedisClient>().Connection,
            tags: ["readiness"]);
    }
}
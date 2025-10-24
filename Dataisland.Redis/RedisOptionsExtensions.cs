using Microsoft.Extensions.Configuration;

namespace Dataisland.Redis;

public static class RedisOptionsExtensions
{
    public static RedisOptions GetRedisCacheOptions(this IConfiguration configuration, string section = "Redis")
    {
        return configuration.GetSection(section).Get<RedisOptions>()!;
    }
}
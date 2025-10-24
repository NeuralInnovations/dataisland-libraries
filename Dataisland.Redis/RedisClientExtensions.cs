using Dataisland.Cache;
using Dataisland.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.Redis;

public static class RedisClientExtensions
{
    public static async Task RunRedisAsync(this IHostStartup startup)
    {
        // Connect Redis
        await Task.WhenAll(tasks:
            startup
                .Services
                .GetServices<IRedisConnection>()
                .Select(x => x.ConnectAsync())
        );

        startup.Configure<IRedisClient>();
    }

    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration config)
    {
        services.AddStartupChecker<IRedisClient>();

        services.AddOptions<RedisOptions>().Bind(config);
        services.AddSingleton<RedisClient>();
        services.AddSingleton<IRedisClient>(s => s.GetRequiredService<RedisClient>());
        services.AddSingleton<IRedisConnection>(s => s.GetRequiredService<RedisClient>());

        return services;
    }

    public static IServiceCollection AddRedisStringCache(this IServiceCollection services)
    {
        services.AddSingleton<RedisStringCache>();

        services.AddSingleton<IStringCache>(s => s.GetRequiredService<RedisStringCache>());
        services.AddSingleton<IStringCacheAsync>(s => s.GetRequiredService<RedisStringCache>());

        return services;
    }
}
using System.Text.Json;

namespace Dataisland.Cache;

public interface ICacheService
{
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null);
    Task InvalidateAsync(string key);
}

public class CacheService(IStringCacheAsync cache) : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        var cached = await cache.GetAsync(key);
        if (cached is not null)
            return JsonSerializer.Deserialize<T>(cached);

        var value = await factory();
        if (value is not null)
        {
            var json = JsonSerializer.Serialize(value);
            await cache.SetAsync(key, json, ttl ?? DefaultTtl);
        }

        return value;
    }

    public Task InvalidateAsync(string key) => cache.DeleteAsync(key);
}

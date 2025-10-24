using Dataisland.Cache;
using StackExchange.Redis;
using When = Dataisland.Cache.When;

namespace Dataisland.Redis;

public class RedisStringCache(IRedisClient client) : IStringCache, IStringCacheAsync
{
    public string? Get(string key)
    {
        var result = client.Database.StringGet(key);
        if (result.IsNull) return null;
        return result.ToString();
    }

    public bool Set(string key, string value, TimeSpan? expiry = null, When when = When.Always)
    {
        return client.Database.StringSet(key, value, expiry, (StackExchange.Redis.When)(int)when);
    }

    public bool Delete(string key)
    {
        return client.Database.KeyDelete(key, CommandFlags.PreferMaster);
    }

    public async Task<string?> GetAsync(string key)
    {
        var result = await client.Database.StringGetAsync(key);
        if (result.IsNull) return null;
        return result.ToString();
    }

    public Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null, When when = When.Always)
    {
        return client.Database.StringSetAsync(key, value, expiry, (StackExchange.Redis.When)(int)when);
    }

    public Task<bool> DeleteAsync(string key)
    {
        return client.Database.KeyDeleteAsync(key, CommandFlags.PreferMaster);
    }
}
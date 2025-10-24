using Dataisland.Cache;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Dataisland.MongoDB.Cache;

public class MongoCache<T> : ICache<T>
{
    private readonly string _name = typeof(T).Name;
    private readonly IStringCacheAsync _cache;
    private readonly string? _prefix;

    public MongoCache(
        IStringCacheAsync cache,
        string? prefix = null
    )
    {
        _cache = cache;
        _prefix = prefix;

        // Register a class map for T only if it's not already registered.
        if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            BsonClassMap.RegisterClassMap<T>(cm =>
            {
                cm.AutoMap(); // Automatically map properties
            });
        }
    }

    public async Task<T?> GetAsync(string key)
    {
        var result = await _cache.GetAsync(Key(key));
        if (result is not null)
        {
            // Fast-path null marker
            if (result == "null")
            {
                return default;
            }

            return await Task.Run<T?>(
                () => BsonSerializer
                    .Deserialize<T>(result)
            );
        }

        return default;
    }

    public async Task<bool> SetAsync(string key, T? value, TimeSpan? expiry = null, When when = When.Always)
    {
        // Serialize the value to extended JSON using MongoDB's serializer.
        // This correctly handles nulls and complex types.
        var result = value is null
            ? "null"
            : await Task.Run(() => BsonExtensionMethods.ToJson(value));

        return await _cache.SetAsync(Key(key), result, expiry, when);
    }

    public Task<bool> DeleteAsync(string key)
    {
        return _cache.DeleteAsync(Key(key));
    }

    private string Key(string key)
    {
        return $"{_prefix ?? ""}{_name}/{key}";
    }
}
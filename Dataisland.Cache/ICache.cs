namespace Dataisland.Cache;

public interface ICache<T>
{
    Task<T?> GetAsync(string key);
    Task<bool> SetAsync(string key, T? value, TimeSpan? expiry = null, When when = When.Always);
    Task<bool> DeleteAsync(string key);
}
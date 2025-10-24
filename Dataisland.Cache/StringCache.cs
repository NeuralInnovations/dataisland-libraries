namespace Dataisland.Cache;

public interface IStringCache
{
    /// <summary>
    /// Get value 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    string? Get(string key);

    /// <summary>
    /// Set value
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="expiry"></param>
    /// <param name="when"></param>
    /// <returns>true if the string was set, false otherwise.</returns>
    bool Set(string key, string value, TimeSpan? expiry = null, When when = When.Always);

    /// <summary>
    /// Delete value
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    bool Delete(string key);
}

public interface IStringCacheAsync
{
    /// <summary>
    /// Get value 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Set value
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="expiry"></param>
    /// <param name="when"></param>
    /// <returns>true if the string was set, false otherwise.</returns>
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null, When when = When.Always);

    /// <summary>
    /// Delete value
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task<bool> DeleteAsync(string key);
}
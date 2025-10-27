namespace CodeLogic.Abstractions;

/// <summary>
/// Abstraction for caching operations
/// </summary>
public interface ICache
{
    /// <summary>
    /// Stores a value in the cache
    /// </summary>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>
    /// Retrieves a value from the cache
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Checks if a key exists in the cache
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Removes a value from the cache
    /// </summary>
    Task<bool> RemoveAsync(string key);

    /// <summary>
    /// Clears all values from the cache
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets or creates a cached value
    /// </summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
}

/// <summary>
/// Cache priority levels
/// </summary>
public enum CachePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    NeverRemove = 3
}

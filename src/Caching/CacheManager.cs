using CodeLogic.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace CodeLogic.Caching;

/// <summary>
/// Multi-tier caching manager with memory cache and optional compression
/// </summary>
public class CacheManager : ICache
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheOptions _options;

    public CacheManager(CacheOptions? options = null)
    {
        _options = options ?? new CacheOptions();
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _options.MemoryCacheSizeLimit
        });
    }

    public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                Size = 1
            };

            if (expiration.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
            }
            else if (_options.DefaultExpiration.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = _options.DefaultExpiration;
            }

            // Compress large values if enabled
            if (_options.EnableCompression && ShouldCompress(value))
            {
                var compressed = CompressionHelper.Compress(value);
                _memoryCache.Set(key, new CompressedCacheEntry { Data = compressed, TypeName = typeof(T).AssemblyQualifiedName! }, cacheOptions);
            }
            else
            {
                _memoryCache.Set(key, value, cacheOptions);
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = _memoryCache.Get(key);

            if (value == null)
                return Task.FromResult<T?>(default);

            // Check if it's a compressed entry
            if (value is CompressedCacheEntry compressedEntry)
            {
                var decompressed = CompressionHelper.Decompress<T>(compressedEntry.Data);
                return Task.FromResult(decompressed);
            }

            if (value is T typedValue)
                return Task.FromResult<T?>(typedValue);

            return Task.FromResult<T?>(default);
        }
        catch
        {
            return Task.FromResult<T?>(default);
        }
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_memoryCache.TryGetValue(key, out _));
    }

    public Task<bool> RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        return Task.FromResult(true);
    }

    public Task ClearAsync()
    {
        if (_memoryCache is MemoryCache memCache)
        {
            memCache.Compact(1.0); // Compact 100% - effectively clears the cache
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        var existing = await GetAsync<T>(key);

        if (existing != null)
            return existing;

        var value = await factory();
        await SetAsync(key, value, expiration);

        return value;
    }

    private bool ShouldCompress<T>(T value)
    {
        if (!_options.EnableCompression)
            return false;

        // Only compress strings and complex objects
        return value is string || (!typeof(T).IsPrimitive && typeof(T) != typeof(string));
    }

    private class CompressedCacheEntry
    {
        public required byte[] Data { get; init; }
        public required string TypeName { get; init; }
    }
}

/// <summary>
/// Configuration options for the cache manager
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Maximum number of cached items (memory cache)
    /// </summary>
    public long MemoryCacheSizeLimit { get; set; } = 10000;

    /// <summary>
    /// Default expiration time for cached items
    /// </summary>
    public TimeSpan? DefaultExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to enable compression for large cache values
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Minimum size (bytes) for compression
    /// </summary>
    public int CompressionThreshold { get; set; } = 1024; // 1 KB
}

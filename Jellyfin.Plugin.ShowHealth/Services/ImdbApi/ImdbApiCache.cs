using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi;

/// <summary>
/// Persistent file-based cache for IMDb API responses.
/// Cache entries expire after 7 days.
/// Per-key locking avoids serialising unrelated concurrent requests.
/// </summary>
public class ImdbApiCache : IDisposable
{
    private const string CacheExtension = ".json";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _cacheDir;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbApiCache"/> class.
    /// </summary>
    /// <param name="cacheDir">Directory to store cache files.</param>
    /// <param name="ttl">Time-to-live for cache entries (default: 7 days).</param>
    public ImdbApiCache(string cacheDir, TimeSpan? ttl = null)
    {
        _cacheDir = cacheDir;
        _ttl = ttl ?? DefaultTtl;
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Gets a cached value by key. Returns default if missing, expired, or corrupt.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            return default;
        }

        var keyLock = GetKeyLock(key);
        await keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return default;
            }

            try
            {
                var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                var cacheEntry = JsonSerializer.Deserialize<CacheEntry<T>>(content, JsonOptions);

                if (cacheEntry == null || cacheEntry.ExpiresAt < DateTimeOffset.UtcNow)
                {
                    File.Delete(filePath);
                    return default;
                }

                return cacheEntry.Value;
            }
            catch (JsonException)
            {
                // Corrupt cache file — treat as a miss and evict.
                TryDeleteFile(filePath);
                return default;
            }
        }
        finally
        {
            keyLock.Release();
        }
    }

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttlOverride">Optional TTL override; uses the configured default when null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetAsync<T>(string key, T value, TimeSpan? ttlOverride = null, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);

        var keyLock = GetKeyLock(key);
        await keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entry = new CacheEntry<T>
            {
                Key = key,
                Value = value,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttlOverride ?? _ttl),
            };

            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            keyLock.Release();
        }
    }

    /// <summary>
    /// Removes a specific key from the cache.
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);

        var keyLock = GetKeyLock(key);
        await keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        finally
        {
            keyLock.Release();
        }
    }

    /// <summary>
    /// Removes all cache entries whose original key starts with the given prefix.
    /// </summary>
    public async Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        foreach (var file in Directory.GetFiles(_cacheDir, $"*{CacheExtension}"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("Key", out var keyProp))
                {
                    var key = keyProp.GetString();
                    if (key != null && key.StartsWith(keyPrefix, StringComparison.Ordinal))
                    {
                        TryDeleteFile(file);
                    }
                }
            }
            catch (JsonException)
            {
                TryDeleteFile(file);
            }
        }
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // Bulk operation: no per-key locks needed — just delete every file.
        foreach (var file in Directory.GetFiles(_cacheDir, $"*{CacheExtension}"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryDeleteFile(file);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes all expired cache entries.
    /// </summary>
    /// <returns>Number of expired entries removed.</returns>
    public async Task<int> CleanExpiredAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;

        foreach (var file in Directory.GetFiles(_cacheDir, $"*{CacheExtension}"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("ExpiresAt", out var expiresProp))
                {
                    var expiresAt = expiresProp.GetDateTimeOffset();
                    if (expiresAt < DateTimeOffset.UtcNow)
                    {
                        TryDeleteFile(file);
                        count++;
                    }
                }
            }
            catch
            {
                TryDeleteFile(file);
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var semaphore in _keyLocks.Values)
                {
                    semaphore.Dispose();
                }

                _keyLocks.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Builds the cache file path from a relative cache key (path + query params, no base URL).
    /// The key is hashed so that any change in query params produces a different file.
    /// </summary>
    private string GetFilePath(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var hashStr = Convert.ToHexString(hash).ToLowerInvariant();
        return Path.Combine(_cacheDir, hashStr + CacheExtension);
    }

    private SemaphoreSlim GetKeyLock(string key)
    {
        return _keyLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
            // Best-effort deletion; ignore errors.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort deletion; ignore errors.
        }
    }

    private sealed class CacheEntry<T>
    {
        public string? Key { get; set; }

        public T? Value { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }
}

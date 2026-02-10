using System.Collections.Concurrent;
using llyrtkframework.Results;
using llyrtkframework.Time;

namespace llyrtkframework.Caching;

/// <summary>
/// インメモリキャッシュの実装
/// </summary>
public class InMemoryCache : ICache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private DateTime _lastCleanup;

    public InMemoryCache(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
        _lastCleanup = _dateTimeProvider.UtcNow;
    }

    public Result<T> Get<T>(string key)
    {
        CleanupExpiredEntriesIfNeeded();

        if (!_cache.TryGetValue(key, out var entry))
            return Result<T>.Failure($"Key '{key}' not found in cache");

        if (entry.IsExpired(_dateTimeProvider))
        {
            _cache.TryRemove(key, out _);
            return Result<T>.Failure($"Key '{key}' has expired");
        }

        try
        {
            var value = (T)entry.Value;
            return Result<T>.Success(value);
        }
        catch (InvalidCastException ex)
        {
            return Result<T>.Failure($"Failed to cast cached value for key '{key}'", ex);
        }
    }

    public Task<Result<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Get<T>(key));
    }

    public Result Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        var expiresAt = expiration.HasValue
            ? _dateTimeProvider.UtcNow.Add(expiration.Value)
            : (DateTime?)null;

        var entry = new CacheEntry(value!, expiresAt);
        _cache[key] = entry;

        return Result.Success();
    }

    public Task<Result> SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Set(key, value, expiration));
    }

    public Result Remove(string key)
    {
        if (_cache.TryRemove(key, out _))
            return Result.Success();

        return Result.Failure($"Key '{key}' not found in cache");
    }

    public Task<Result> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Remove(key));
    }

    public bool Exists(string key)
    {
        if (!_cache.TryGetValue(key, out var entry))
            return false;

        if (entry.IsExpired(_dateTimeProvider))
        {
            _cache.TryRemove(key, out _);
            return false;
        }

        return true;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Exists(key));
    }

    public Result Clear()
    {
        _cache.Clear();
        return Result.Success();
    }

    public Task<Result> ClearAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Clear());
    }

    /// <summary>
    /// 期限切れのエントリをクリーンアップします
    /// </summary>
    public void CleanupExpiredEntries()
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired(_dateTimeProvider))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        _lastCleanup = _dateTimeProvider.UtcNow;
    }

    private void CleanupExpiredEntriesIfNeeded()
    {
        // 5分ごとにクリーンアップ
        if (_dateTimeProvider.UtcNow - _lastCleanup < TimeSpan.FromMinutes(5))
            return;

        if (!_cleanupLock.Wait(0))
            return;

        try
        {
            CleanupExpiredEntries();
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    private class CacheEntry
    {
        public object Value { get; }
        public DateTime? ExpiresAt { get; }

        public CacheEntry(object value, DateTime? expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
        }

        public bool IsExpired(IDateTimeProvider dateTimeProvider)
        {
            return ExpiresAt.HasValue && dateTimeProvider.UtcNow > ExpiresAt.Value;
        }
    }
}

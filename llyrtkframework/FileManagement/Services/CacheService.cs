namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// キャッシュ管理を提供するサービス実装
/// </summary>
public class CacheService<T> : IAutoSaveService<T> where T : class
{
    private T? _cachedData;
    private readonly object _cacheLock = new();

    public T? GetCachedData()
    {
        lock (_cacheLock)
        {
            return _cachedData;
        }
    }

    public void UpdateCache(T data)
    {
        lock (_cacheLock)
        {
            _cachedData = data;
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedData = null;
        }
    }

    public bool HasCachedData
    {
        get
        {
            lock (_cacheLock)
            {
                return _cachedData != null;
            }
        }
    }
}

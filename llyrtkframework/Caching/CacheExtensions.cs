using llyrtkframework.Results;

namespace llyrtkframework.Caching;

/// <summary>
/// キャッシュの拡張メソッド
/// </summary>
public static class CacheExtensions
{
    /// <summary>
    /// キャッシュから値を取得し、存在しない場合はファクトリ関数で生成してキャッシュに保存します
    /// </summary>
    public static Result<T> GetOrSet<T>(
        this ICache cache,
        string key,
        Func<T> factory,
        TimeSpan? expiration = null)
    {
        var result = cache.Get<T>(key);

        if (result.IsSuccess)
            return result;

        try
        {
            var value = factory();
            cache.Set(key, value, expiration);
            return Result<T>.Success(value);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Failed to create value for key '{key}'", ex);
        }
    }

    /// <summary>
    /// 非同期でキャッシュから値を取得し、存在しない場合はファクトリ関数で生成してキャッシュに保存します
    /// </summary>
    public static async Task<Result<T>> GetOrSetAsync<T>(
        this ICache cache,
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var result = await cache.GetAsync<T>(key, cancellationToken);

        if (result.IsSuccess)
            return result;

        try
        {
            var value = await factory();
            await cache.SetAsync(key, value, expiration, cancellationToken);
            return Result<T>.Success(value);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Failed to create value for key '{key}'", ex);
        }
    }

    /// <summary>
    /// キャッシュから値を取得し、存在しない場合は Result を返すファクトリ関数で生成してキャッシュに保存します
    /// </summary>
    public static Result<T> GetOrSetWithResult<T>(
        this ICache cache,
        string key,
        Func<Result<T>> factory,
        TimeSpan? expiration = null)
    {
        var cacheResult = cache.Get<T>(key);

        if (cacheResult.IsSuccess)
            return cacheResult;

        var factoryResult = factory();

        if (factoryResult.IsFailure)
            return factoryResult;

        cache.Set(key, factoryResult.Value, expiration);
        return factoryResult;
    }

    /// <summary>
    /// 非同期でキャッシュから値を取得し、存在しない場合は Result を返すファクトリ関数で生成してキャッシュに保存します
    /// </summary>
    public static async Task<Result<T>> GetOrSetWithResultAsync<T>(
        this ICache cache,
        string key,
        Func<Task<Result<T>>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var cacheResult = await cache.GetAsync<T>(key, cancellationToken);

        if (cacheResult.IsSuccess)
            return cacheResult;

        var factoryResult = await factory();

        if (factoryResult.IsFailure)
            return factoryResult;

        await cache.SetAsync(key, factoryResult.Value, expiration, cancellationToken);
        return factoryResult;
    }

    /// <summary>
    /// 複数のキーの値をバッチで取得します
    /// </summary>
    public static async Task<Dictionary<string, Result<T>>> GetManyAsync<T>(
        this ICache cache,
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var tasks = keys.Select(async key =>
        {
            var result = await cache.GetAsync<T>(key, cancellationToken);
            return (key, result);
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(
            x => x.key,
            x => x.result
        );
    }

    /// <summary>
    /// 複数のキーの値をバッチで設定します
    /// </summary>
    public static async Task<Result> SetManyAsync<T>(
        this ICache cache,
        Dictionary<string, T> items,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = items.Select(kvp =>
            cache.SetAsync(kvp.Key, kvp.Value, expiration, cancellationToken)
        );

        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
            return Result.Failure($"Failed to set some cache entries: {errorMessages}");
        }

        return Result.Success();
    }

    /// <summary>
    /// パターンに一致するキーをすべて削除します（インメモリキャッシュのみ）
    /// </summary>
    public static Result RemoveByPattern(this InMemoryCache cache, string pattern)
    {
        // この機能は InMemoryCache 専用のため、基底インターフェースには含めない
        // 実装はキャッシュの種類によって異なる可能性がある
        throw new NotImplementedException("Pattern-based removal requires specific implementation");
    }

    /// <summary>
    /// キャッシュキーを生成するヘルパーメソッド
    /// </summary>
    public static string GenerateKey(string prefix, params object[] parameters)
    {
        var paramString = string.Join(":", parameters.Select(p => p?.ToString() ?? "null"));
        return $"{prefix}:{paramString}";
    }

    /// <summary>
    /// Result に対してキャッシュを適用します
    /// </summary>
    public static async Task<Result<T>> WithCacheAsync<T>(
        this Task<Result<T>> resultTask,
        ICache cache,
        string key,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        // まずキャッシュをチェック
        var cachedResult = await cache.GetAsync<T>(key, cancellationToken);
        if (cachedResult.IsSuccess)
            return cachedResult;

        // キャッシュミスの場合、元の操作を実行
        var result = await resultTask;

        // 成功した場合のみキャッシュに保存
        if (result.IsSuccess)
        {
            await cache.SetAsync(key, result.Value, expiration, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Result に対してキャッシュを適用します（同期版）
    /// </summary>
    public static Result<T> WithCache<T>(
        this Result<T> result,
        ICache cache,
        string key,
        TimeSpan? expiration = null)
    {
        if (result.IsSuccess)
        {
            cache.Set(key, result.Value, expiration);
        }

        return result;
    }
}

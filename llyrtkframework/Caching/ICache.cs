using llyrtkframework.Results;

namespace llyrtkframework.Caching;

/// <summary>
/// キャッシュのインターフェース
/// </summary>
public interface ICache
{
    /// <summary>
    /// キャッシュから値を取得します
    /// </summary>
    Result<T> Get<T>(string key);

    /// <summary>
    /// 非同期でキャッシュから値を取得します
    /// </summary>
    Task<Result<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// キャッシュに値を設定します
    /// </summary>
    Result Set<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>
    /// 非同期でキャッシュに値を設定します
    /// </summary>
    Task<Result> SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// キャッシュから値を削除します
    /// </summary>
    Result Remove(string key);

    /// <summary>
    /// 非同期でキャッシュから値を削除します
    /// </summary>
    Task<Result> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// キャッシュに指定されたキーが存在するか確認します
    /// </summary>
    bool Exists(string key);

    /// <summary>
    /// 非同期でキャッシュに指定されたキーが存在するか確認します
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// キャッシュをクリアします
    /// </summary>
    Result Clear();

    /// <summary>
    /// 非同期でキャッシュをクリアします
    /// </summary>
    Task<Result> ClearAsync(CancellationToken cancellationToken = default);
}

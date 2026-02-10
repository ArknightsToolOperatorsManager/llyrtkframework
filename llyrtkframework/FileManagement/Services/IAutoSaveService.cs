namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// 自動保存とキャッシュ管理を提供するサービス
/// </summary>
public interface IAutoSaveService<T> where T : class
{
    /// <summary>
    /// キャッシュされたデータを取得
    /// </summary>
    T? GetCachedData();

    /// <summary>
    /// キャッシュを更新
    /// </summary>
    /// <param name="data">キャッシュするデータ</param>
    void UpdateCache(T data);

    /// <summary>
    /// キャッシュをクリア
    /// </summary>
    void ClearCache();

    /// <summary>
    /// キャッシュされたデータがあるか
    /// </summary>
    bool HasCachedData { get; }
}

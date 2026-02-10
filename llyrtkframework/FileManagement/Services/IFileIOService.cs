using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// ファイルI/O操作を提供するサービス
/// </summary>
public interface IFileIOService<T> where T : class
{
    /// <summary>
    /// ファイルからデータを非同期で読み込みます
    /// </summary>
    /// <param name="onSuccess">読み込み成功時のコールバック</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result<T>> LoadAsync(
        Action<T>? onSuccess = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// データをファイルに非同期で保存します
    /// </summary>
    /// <param name="data">保存するデータ</param>
    /// <param name="onSuccess">保存成功時のコールバック</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result> SaveAsync(
        T data,
        Action? onSuccess = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ファイルからデータを同期読み込みします
    /// </summary>
    /// <param name="onSuccess">読み込み成功時のコールバック</param>
    Result<T> Load(Action<T>? onSuccess = null);

    /// <summary>
    /// データをファイルに同期保存します
    /// </summary>
    /// <param name="data">保存するデータ</param>
    /// <param name="onSuccess">保存成功時のコールバック</param>
    Result Save(T data, Action? onSuccess = null);
}

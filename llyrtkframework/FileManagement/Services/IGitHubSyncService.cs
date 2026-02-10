using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// GitHub同期操作を提供するサービス
/// </summary>
public interface IGitHubSyncService<T> where T : class
{
    /// <summary>
    /// GitHubからファイルをダウンロードして保存します（強制ダウンロード）
    /// </summary>
    /// <param name="onSuccess">ダウンロード成功時のコールバック</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result<T>> DownloadFromGitHubAsync(
        Action<T>? onSuccess = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GitHub上のファイルと同期（差分チェック＆更新）します
    /// </summary>
    /// <param name="onSuccess">同期成功時のコールバック</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result<T>> SyncWithGitHubAsync(
        Action<T>? onSuccess = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GitHub同期が有効かどうか
    /// </summary>
    bool IsEnabled { get; }
}

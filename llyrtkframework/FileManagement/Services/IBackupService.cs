using llyrtkframework.FileManagement.Backup;
using llyrtkframework.FileManagement.Core;
using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// バックアップ操作を提供するサービス
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// バックアップを作成します
    /// </summary>
    /// <param name="onSuccess">バックアップ成功時のコールバック（最新バックアップパスが渡される）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result> CreateBackupAsync(
        Action<string>? onSuccess = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 最新のバックアップの内容を取得します
    /// </summary>
    Task<Result<string>> GetLatestBackupContentAsync();

    /// <summary>
    /// ロールバック機能付きでバックアップ内容を取得します
    /// </summary>
    /// <param name="options">ロールバックオプション</param>
    /// <param name="onRollback">ロールバック発生時のコールバック</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result<string>> GetBackupContentWithRollbackAsync(
        RollbackOptions? options = null,
        Action<RollbackInfo>? onRollback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// ロールバック情報
/// </summary>
public class RollbackInfo
{
    public List<string> TriedBackupPaths { get; init; } = new();
    public string? SuccessfulBackupPath { get; init; }
    public List<string> FailureReasons { get; init; } = new();
}

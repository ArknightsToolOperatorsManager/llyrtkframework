using llyrtkframework.FileManagement.Diff;

namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// GitHubファイル同期完了イベント
/// </summary>
public class GitHubFileUpdatedEvent
{
    /// <summary>ローカルファイルパス</summary>
    public required string LocalFilePath { get; init; }

    /// <summary>ローカルバックアップパス</summary>
    public required string BackupFilePath { get; init; }

    /// <summary>リモートURL</summary>
    public required string RemoteUrl { get; init; }

    /// <summary>JSON差分レポート（JSONファイル以外の場合はnull）</summary>
    public JsonDiffReport? DiffReport { get; init; }

    /// <summary>更新日時</summary>
    public DateTime UpdatedAt { get; init; } = DateTime.Now;
}

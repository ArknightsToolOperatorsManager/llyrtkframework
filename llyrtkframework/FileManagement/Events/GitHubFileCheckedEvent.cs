namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// GitHub確認完了イベント（変更なし時も発行）
/// </summary>
public class GitHubFileCheckedEvent
{
    /// <summary>ローカルファイルパス</summary>
    public required string LocalFilePath { get; init; }

    /// <summary>リモートURL</summary>
    public required string RemoteUrl { get; init; }

    /// <summary>変更が検出されたか</summary>
    public bool HasChanges { get; init; }

    /// <summary>チェック日時</summary>
    public DateTime CheckedAt { get; init; } = DateTime.Now;
}

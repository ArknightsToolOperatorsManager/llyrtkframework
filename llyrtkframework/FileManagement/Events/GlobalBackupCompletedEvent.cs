namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// グローバルバックアップ完了イベント（全ファイルのバックアップ完了時）
/// </summary>
public class GlobalBackupCompletedEvent
{
    /// <summary>バックアップされたファイル数</summary>
    public int FileCount { get; init; }

    /// <summary>バックアップ完了日時</summary>
    public DateTime CompletedAt { get; init; }

    public GlobalBackupCompletedEvent(int fileCount)
    {
        FileCount = fileCount;
        CompletedAt = DateTime.Now;
    }
}

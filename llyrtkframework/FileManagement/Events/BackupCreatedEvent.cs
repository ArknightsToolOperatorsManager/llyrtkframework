namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// バックアップ作成完了イベント
/// </summary>
public class BackupCreatedEvent
{
    /// <summary>元のファイルパス</summary>
    public string OriginalFilePath { get; init; }

    /// <summary>バックアップファイルパス</summary>
    public string BackupFilePath { get; init; }

    /// <summary>バックアップ作成日時</summary>
    public DateTime CreatedAt { get; init; }

    public BackupCreatedEvent(string originalFilePath, string backupFilePath)
    {
        OriginalFilePath = originalFilePath;
        BackupFilePath = backupFilePath;
        CreatedAt = DateTime.Now;
    }
}

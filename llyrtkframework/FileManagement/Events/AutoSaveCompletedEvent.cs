namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// 自動保存完了イベント
/// </summary>
public class AutoSaveCompletedEvent
{
    public required string FilePath { get; init; }
    public DateTime SavedAt { get; init; }
}

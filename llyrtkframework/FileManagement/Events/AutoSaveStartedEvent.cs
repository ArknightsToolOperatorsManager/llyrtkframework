namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// 自動保存開始イベント
/// </summary>
public class AutoSaveStartedEvent
{
    public int FileCount { get; init; }
    public DateTime StartedAt { get; init; }
}

namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// 自動保存失敗イベント
/// </summary>
public class AutoSaveFailedEvent
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
    public DateTime FailedAt { get; init; }
}

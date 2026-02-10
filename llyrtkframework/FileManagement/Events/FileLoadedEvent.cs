namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// ファイル読み込み完了イベント
/// </summary>
public class FileLoadedEvent
{
    /// <summary>読み込まれたファイルのパス</summary>
    public string FilePath { get; init; }

    /// <summary>読み込み日時</summary>
    public DateTime LoadedAt { get; init; }

    public FileLoadedEvent(string filePath)
    {
        FilePath = filePath;
        LoadedAt = DateTime.Now;
    }
}

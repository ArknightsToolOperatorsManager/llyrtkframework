namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// ファイル保存完了イベント
/// </summary>
public class FileSavedEvent
{
    /// <summary>保存されたファイルのパス</summary>
    public string FilePath { get; init; }

    /// <summary>保存日時</summary>
    public DateTime SavedAt { get; init; }

    public FileSavedEvent(string filePath)
    {
        FilePath = filePath;
        SavedAt = DateTime.Now;
    }
}

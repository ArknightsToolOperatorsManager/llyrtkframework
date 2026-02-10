namespace llyrtkframework.FileManagement.Diff;

/// <summary>
/// JSON変更タイプ
/// </summary>
public enum JsonChangeType
{
    /// <summary>プロパティが追加された</summary>
    Added,

    /// <summary>プロパティが削除された</summary>
    Removed,

    /// <summary>プロパティの値が変更された</summary>
    Modified
}

/// <summary>
/// 個別プロパティの変更情報
/// </summary>
public class JsonPropertyChange
{
    /// <summary>プロパティパス（例: "users[0].name"）</summary>
    public required string PropertyPath { get; init; }

    /// <summary>変更タイプ</summary>
    public required JsonChangeType ChangeType { get; init; }

    /// <summary>古い値（Added時はnull）</summary>
    public object? OldValue { get; init; }

    /// <summary>新しい値（Removed時はnull）</summary>
    public object? NewValue { get; init; }

    public override string ToString()
    {
        return ChangeType switch
        {
            JsonChangeType.Added => $"Added: {PropertyPath} = {NewValue}",
            JsonChangeType.Removed => $"Removed: {PropertyPath} (was {OldValue})",
            JsonChangeType.Modified => $"Modified: {PropertyPath} from {OldValue} to {NewValue}",
            _ => $"{ChangeType}: {PropertyPath}"
        };
    }
}

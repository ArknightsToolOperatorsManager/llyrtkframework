namespace llyrtkframework.FileManagement.Diff;

/// <summary>
/// JSON差分レポート
/// </summary>
public class JsonDiffReport
{
    /// <summary>すべての変更</summary>
    public List<JsonPropertyChange> Changes { get; init; } = new();

    /// <summary>変更があるか</summary>
    public bool HasChanges => Changes.Any();

    /// <summary>追加されたプロパティのリスト</summary>
    public List<JsonPropertyChange> AddedProperties =>
        Changes.Where(c => c.ChangeType == JsonChangeType.Added).ToList();

    /// <summary>削除されたプロパティのリスト</summary>
    public List<JsonPropertyChange> RemovedProperties =>
        Changes.Where(c => c.ChangeType == JsonChangeType.Removed).ToList();

    /// <summary>変更されたプロパティのリスト</summary>
    public List<JsonPropertyChange> ModifiedProperties =>
        Changes.Where(c => c.ChangeType == JsonChangeType.Modified).ToList();

    /// <summary>
    /// 変更数の取得
    /// </summary>
    public int ChangeCount => Changes.Count;

    /// <summary>
    /// サマリー文字列の取得
    /// </summary>
    public string GetSummary()
    {
        if (!HasChanges)
            return "No changes detected";

        return $"Changes: {AddedProperties.Count} added, " +
               $"{RemovedProperties.Count} removed, " +
               $"{ModifiedProperties.Count} modified";
    }

    public override string ToString() => GetSummary();
}

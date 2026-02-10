using System.Text.Json;

namespace llyrtkframework.FileManagement.Diff;

/// <summary>
/// 汎用JSON差分検出器
/// </summary>
public static class JsonDiffDetector
{
    /// <summary>
    /// 2つのJSON文字列間の差分を検出します
    /// </summary>
    /// <param name="oldJson">古いJSON文字列</param>
    /// <param name="newJson">新しいJSON文字列</param>
    /// <returns>差分レポート</returns>
    public static JsonDiffReport DetectDifferences(string? oldJson, string? newJson)
    {
        var report = new JsonDiffReport();

        // 両方nullまたは空の場合
        if (string.IsNullOrWhiteSpace(oldJson) && string.IsNullOrWhiteSpace(newJson))
            return report;

        try
        {
            // oldがnullまたは空の場合、newの全プロパティが追加
            if (string.IsNullOrWhiteSpace(oldJson))
            {
                if (!string.IsNullOrWhiteSpace(newJson))
                {
                    using var newDoc = JsonDocument.Parse(newJson);
                    AddAllPropertiesAsAdded(newDoc.RootElement, "", report.Changes);
                }
                return report;
            }

            // newがnullまたは空の場合、oldの全プロパティが削除
            if (string.IsNullOrWhiteSpace(newJson))
            {
                using var oldDoc = JsonDocument.Parse(oldJson);
                AddAllPropertiesAsRemoved(oldDoc.RootElement, "", report.Changes);
                return report;
            }

            // 両方存在する場合、再帰的に比較
            using var oldDocument = JsonDocument.Parse(oldJson);
            using var newDocument = JsonDocument.Parse(newJson);

            CompareElements(oldDocument.RootElement, newDocument.RootElement, "", report.Changes);

            return report;
        }
        catch (JsonException)
        {
            // JSONパースエラーの場合は空のレポートを返す
            return new JsonDiffReport();
        }
    }

    private static void CompareElements(
        JsonElement oldElement,
        JsonElement newElement,
        string path,
        List<JsonPropertyChange> changes)
    {
        // 型が異なる場合は変更として扱う
        if (oldElement.ValueKind != newElement.ValueKind)
        {
            changes.Add(new JsonPropertyChange
            {
                PropertyPath = path == "" ? "$" : path,
                ChangeType = JsonChangeType.Modified,
                OldValue = GetElementValue(oldElement),
                NewValue = GetElementValue(newElement)
            });
            return;
        }

        switch (oldElement.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(oldElement, newElement, path, changes);
                break;

            case JsonValueKind.Array:
                CompareArrays(oldElement, newElement, path, changes);
                break;

            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                CompareValues(oldElement, newElement, path, changes);
                break;
        }
    }

    private static void CompareObjects(
        JsonElement oldElement,
        JsonElement newElement,
        string path,
        List<JsonPropertyChange> changes)
    {
        var oldProperties = oldElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var newProperties = newElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        // 削除されたプロパティ
        foreach (var oldProp in oldProperties)
        {
            if (!newProperties.ContainsKey(oldProp.Key))
            {
                var propPath = BuildPath(path, oldProp.Key);
                changes.Add(new JsonPropertyChange
                {
                    PropertyPath = propPath,
                    ChangeType = JsonChangeType.Removed,
                    OldValue = GetElementValue(oldProp.Value),
                    NewValue = null
                });
            }
        }

        // 追加されたプロパティ
        foreach (var newProp in newProperties)
        {
            if (!oldProperties.ContainsKey(newProp.Key))
            {
                var propPath = BuildPath(path, newProp.Key);
                changes.Add(new JsonPropertyChange
                {
                    PropertyPath = propPath,
                    ChangeType = JsonChangeType.Added,
                    OldValue = null,
                    NewValue = GetElementValue(newProp.Value)
                });
            }
        }

        // 共通プロパティの比較
        foreach (var key in oldProperties.Keys.Intersect(newProperties.Keys))
        {
            var propPath = BuildPath(path, key);
            CompareElements(oldProperties[key], newProperties[key], propPath, changes);
        }
    }

    private static void CompareArrays(
        JsonElement oldElement,
        JsonElement newElement,
        string path,
        List<JsonPropertyChange> changes)
    {
        var oldArray = oldElement.EnumerateArray().ToList();
        var newArray = newElement.EnumerateArray().ToList();

        var maxLength = Math.Max(oldArray.Count, newArray.Count);

        for (int i = 0; i < maxLength; i++)
        {
            var indexPath = $"{path}[{i}]";

            if (i >= oldArray.Count)
            {
                // 新しい要素が追加された
                changes.Add(new JsonPropertyChange
                {
                    PropertyPath = indexPath,
                    ChangeType = JsonChangeType.Added,
                    OldValue = null,
                    NewValue = GetElementValue(newArray[i])
                });
            }
            else if (i >= newArray.Count)
            {
                // 古い要素が削除された
                changes.Add(new JsonPropertyChange
                {
                    PropertyPath = indexPath,
                    ChangeType = JsonChangeType.Removed,
                    OldValue = GetElementValue(oldArray[i]),
                    NewValue = null
                });
            }
            else
            {
                // 要素の比較
                CompareElements(oldArray[i], newArray[i], indexPath, changes);
            }
        }
    }

    private static void CompareValues(
        JsonElement oldElement,
        JsonElement newElement,
        string path,
        List<JsonPropertyChange> changes)
    {
        var oldValue = GetElementValue(oldElement);
        var newValue = GetElementValue(newElement);

        if (!Equals(oldValue, newValue))
        {
            changes.Add(new JsonPropertyChange
            {
                PropertyPath = path == "" ? "$" : path,
                ChangeType = JsonChangeType.Modified,
                OldValue = oldValue,
                NewValue = newValue
            });
        }
    }

    private static void AddAllPropertiesAsAdded(
        JsonElement element,
        string path,
        List<JsonPropertyChange> changes)
    {
        var currentPath = path == "" ? "$" : path;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var propPath = BuildPath(path, prop.Name);
                    AddAllPropertiesAsAdded(prop.Value, propPath, changes);
                }
                break;

            case JsonValueKind.Array:
                var array = element.EnumerateArray().ToList();
                for (int i = 0; i < array.Count; i++)
                {
                    var indexPath = $"{path}[{i}]";
                    AddAllPropertiesAsAdded(array[i], indexPath, changes);
                }
                break;

            default:
                changes.Add(new JsonPropertyChange
                {
                    PropertyPath = currentPath,
                    ChangeType = JsonChangeType.Added,
                    OldValue = null,
                    NewValue = GetElementValue(element)
                });
                break;
        }
    }

    private static void AddAllPropertiesAsRemoved(
        JsonElement element,
        string path,
        List<JsonPropertyChange> changes)
    {
        var currentPath = path == "" ? "$" : path;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var propPath = BuildPath(path, prop.Name);
                    AddAllPropertiesAsRemoved(prop.Value, propPath, changes);
                }
                break;

            case JsonValueKind.Array:
                var array = element.EnumerateArray().ToList();
                for (int i = 0; i < array.Count; i++)
                {
                    var indexPath = $"{path}[{i}]";
                    AddAllPropertiesAsRemoved(array[i], indexPath, changes);
                }
                break;

            default:
                changes.Add(new JsonPropertyChange
                {
                    PropertyPath = currentPath,
                    ChangeType = JsonChangeType.Removed,
                    OldValue = GetElementValue(element),
                    NewValue = null
                });
                break;
        }
    }

    private static string BuildPath(string basePath, string propertyName)
    {
        if (string.IsNullOrEmpty(basePath) || basePath == "$")
            return propertyName;

        return $"{basePath}.{propertyName}";
    }

    private static object? GetElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => "<object>",
            JsonValueKind.Array => "<array>",
            _ => element.ToString()
        };
    }
}

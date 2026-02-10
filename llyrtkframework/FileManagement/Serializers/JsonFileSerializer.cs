using llyrtkframework.FileManagement.Core;
using System.Text.Json;

namespace llyrtkframework.FileManagement.Serializers;

/// <summary>
/// JSON形式のファイルシリアライザー
/// </summary>
/// <typeparam name="T">シリアライズする型</typeparam>
public class JsonFileSerializer<T> : IFileSerializer<T> where T : class
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// デフォルトのオプションでJSONシリアライザーを作成します
    /// </summary>
    public JsonFileSerializer() : this(null)
    {
    }

    /// <summary>
    /// カスタムオプションでJSONシリアライザーを作成します
    /// </summary>
    /// <param name="options">JSONシリアライザーオプション（nullの場合はデフォルト）</param>
    public JsonFileSerializer(JsonSerializerOptions? options)
    {
        _options = options ?? new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public string Serialize(T data)
    {
        return JsonSerializer.Serialize(data, _options);
    }

    public T Deserialize(string content)
    {
        return JsonSerializer.Deserialize<T>(content, _options)
            ?? throw new InvalidOperationException("Deserialization returned null");
    }
}

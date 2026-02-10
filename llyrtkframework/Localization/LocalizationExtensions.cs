using llyrtkframework.Results;

namespace llyrtkframework.Localization;

/// <summary>
/// Localization関連の拡張メソッド
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// ローカライズされた文字列を取得します（拡張メソッド版）
    /// </summary>
    public static string Localize(this string key, ILocalizationService service)
    {
        var result = service.GetString(key);
        return result.IsSuccess ? result.Value! : $"[{key}]";
    }

    /// <summary>
    /// ローカライズされた文字列を取得します（フォーマット付き）
    /// </summary>
    public static string Localize(this string key, ILocalizationService service, params object[] args)
    {
        var result = service.GetString(key, args);
        return result.IsSuccess ? result.Value! : $"[{key}]";
    }

    /// <summary>
    /// 複数のキーに対してローカライズされた文字列を取得します
    /// </summary>
    public static Dictionary<string, string> LocalizeMany(this IEnumerable<string> keys, ILocalizationService service)
    {
        var result = new Dictionary<string, string>();

        foreach (var key in keys)
        {
            var localizedResult = service.GetString(key);
            result[key] = localizedResult.IsSuccess ? localizedResult.Value! : $"[{key}]";
        }

        return result;
    }
}

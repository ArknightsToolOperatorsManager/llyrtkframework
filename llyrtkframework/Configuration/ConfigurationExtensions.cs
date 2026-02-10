using llyrtkframework.Results;

namespace llyrtkframework.Configuration;

/// <summary>
/// Configuration関連の拡張メソッド
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// 設定値を取得し、存在しない場合は設定します
    /// </summary>
    public static Result<T> GetOrSetValue<T>(this IConfigurationManager config, string key, T defaultValue)
    {
        var result = config.GetValue<T>(key);
        if (result.IsSuccess)
            return result;

        var setResult = config.SetValue(key, defaultValue);
        return setResult.IsSuccess
            ? Result<T>.Success(defaultValue)
            : Result<T>.Failure(setResult.ErrorMessage ?? "Unknown error");
    }

    /// <summary>
    /// 複数の設定値を一括取得します
    /// </summary>
    public static Result<Dictionary<string, T>> GetValues<T>(this IConfigurationManager config, params string[] keys)
    {
        var result = new Dictionary<string, T>();

        foreach (var key in keys)
        {
            var valueResult = config.GetValue<T>(key);
            if (valueResult.IsSuccess && valueResult.Value != null)
            {
                result[key] = valueResult.Value;
            }
        }

        return Result<Dictionary<string, T>>.Success(result);
    }

    /// <summary>
    /// 複数の設定値を一括設定します
    /// </summary>
    public static Result SetValues<T>(this IConfigurationManager config, Dictionary<string, T> values)
    {
        foreach (var kvp in values)
        {
            var result = config.SetValue(kvp.Key, kvp.Value);
            if (result.IsFailure)
                return result;
        }

        return Result.Success();
    }
}

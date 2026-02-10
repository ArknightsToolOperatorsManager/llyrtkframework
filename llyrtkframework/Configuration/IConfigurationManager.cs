using llyrtkframework.Results;

namespace llyrtkframework.Configuration;

/// <summary>
/// アプリケーション設定管理のインターフェース
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// 設定値を取得します
    /// </summary>
    Result<T> GetValue<T>(string key, T defaultValue = default!);

    /// <summary>
    /// 設定値を設定します
    /// </summary>
    Result SetValue<T>(string key, T value);

    /// <summary>
    /// 設定が存在するかチェックします
    /// </summary>
    bool ContainsKey(string key);

    /// <summary>
    /// 設定を削除します
    /// </summary>
    Result RemoveValue(string key);

    /// <summary>
    /// すべての設定をクリアします
    /// </summary>
    Result Clear();

    /// <summary>
    /// 設定をファイルに保存します
    /// </summary>
    Task<Result> SaveAsync();

    /// <summary>
    /// 設定をファイルから読み込みます
    /// </summary>
    Task<Result> LoadAsync();

    /// <summary>
    /// 設定をリセットしてデフォルト値に戻します
    /// </summary>
    Result Reset();
}

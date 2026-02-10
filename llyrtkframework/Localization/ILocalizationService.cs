using llyrtkframework.Results;
using System.Globalization;

namespace llyrtkframework.Localization;

/// <summary>
/// ローカライゼーションサービスのインターフェース
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 現在のカルチャ
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// カルチャを設定します
    /// </summary>
    Result SetCulture(CultureInfo culture);

    /// <summary>
    /// カルチャを設定します（言語コード）
    /// </summary>
    Result SetCulture(string cultureName);

    /// <summary>
    /// ローカライズされた文字列を取得します
    /// </summary>
    Result<string> GetString(string key);

    /// <summary>
    /// ローカライズされた文字列を取得します（フォーマット付き）
    /// </summary>
    Result<string> GetString(string key, params object[] args);

    /// <summary>
    /// 文字列が存在するかチェックします
    /// </summary>
    bool ContainsKey(string key);

    /// <summary>
    /// 利用可能なカルチャ一覧を取得します
    /// </summary>
    IEnumerable<CultureInfo> GetAvailableCultures();

    /// <summary>
    /// カルチャ変更イベント
    /// </summary>
    IObservable<CultureInfo> CultureChanged { get; }
}

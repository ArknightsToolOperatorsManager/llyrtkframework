namespace llyrtkframework.Time;

/// <summary>
/// 日時を提供するインターフェース
/// テスタビリティのために DateTime.Now/UtcNow を抽象化します
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// 現在の UTC 日時を取得します
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// 現在のローカル日時を取得します
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// 現在の日付（時刻は00:00:00）を取得します
    /// </summary>
    DateTime Today { get; }
}

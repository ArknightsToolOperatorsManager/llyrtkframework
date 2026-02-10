namespace llyrtkframework.Time;

/// <summary>
/// システム時刻を返す IDateTimeProvider の実装
/// </summary>
public class SystemDateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// 現在の UTC 日時を取得します
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// 現在のローカル日時を取得します
    /// </summary>
    public DateTime Now => DateTime.Now;

    /// <summary>
    /// 現在の日付（時刻は00:00:00）を取得します
    /// </summary>
    public DateTime Today => DateTime.Today;
}

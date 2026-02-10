namespace llyrtkframework.Time;

/// <summary>
/// 固定された日時を返す IDateTimeProvider の実装
/// テスト用に使用します
/// </summary>
public class FixedDateTimeProvider : IDateTimeProvider
{
    private readonly DateTime _fixedDateTime;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="fixedDateTime">固定する日時</param>
    public FixedDateTimeProvider(DateTime fixedDateTime)
    {
        _fixedDateTime = fixedDateTime;
    }

    /// <summary>
    /// 固定された UTC 日時を取得します
    /// </summary>
    public DateTime UtcNow => _fixedDateTime.Kind == DateTimeKind.Utc
        ? _fixedDateTime
        : _fixedDateTime.ToUniversalTime();

    /// <summary>
    /// 固定されたローカル日時を取得します
    /// </summary>
    public DateTime Now => _fixedDateTime.Kind == DateTimeKind.Local
        ? _fixedDateTime
        : _fixedDateTime.ToLocalTime();

    /// <summary>
    /// 固定された日付（時刻は00:00:00）を取得します
    /// </summary>
    public DateTime Today => _fixedDateTime.Date;
}

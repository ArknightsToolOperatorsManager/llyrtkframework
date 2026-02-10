namespace llyrtkframework.Time;

/// <summary>
/// 調整可能な日時を返す IDateTimeProvider の実装
/// テスト用に時刻を進めたり戻したりできます
/// </summary>
public class AdjustableDateTimeProvider : IDateTimeProvider
{
    private DateTime _baseDateTime;
    private TimeSpan _offset;

    /// <summary>
    /// コンストラクタ（現在時刻を基準にします）
    /// </summary>
    public AdjustableDateTimeProvider()
    {
        _baseDateTime = DateTime.UtcNow;
        _offset = TimeSpan.Zero;
    }

    /// <summary>
    /// コンストラクタ（指定された日時を基準にします）
    /// </summary>
    /// <param name="baseDateTime">基準日時</param>
    public AdjustableDateTimeProvider(DateTime baseDateTime)
    {
        _baseDateTime = baseDateTime.Kind == DateTimeKind.Utc
            ? baseDateTime
            : baseDateTime.ToUniversalTime();
        _offset = TimeSpan.Zero;
    }

    /// <summary>
    /// 現在の UTC 日時を取得します
    /// </summary>
    public DateTime UtcNow => _baseDateTime.Add(_offset);

    /// <summary>
    /// 現在のローカル日時を取得します
    /// </summary>
    public DateTime Now => UtcNow.ToLocalTime();

    /// <summary>
    /// 現在の日付（時刻は00:00:00）を取得します
    /// </summary>
    public DateTime Today => UtcNow.Date;

    /// <summary>
    /// 時刻を指定された期間進めます
    /// </summary>
    /// <param name="timeSpan">進める期間</param>
    public void Advance(TimeSpan timeSpan)
    {
        _offset = _offset.Add(timeSpan);
    }

    /// <summary>
    /// 時刻を指定された期間戻します
    /// </summary>
    /// <param name="timeSpan">戻す期間</param>
    public void Rewind(TimeSpan timeSpan)
    {
        _offset = _offset.Subtract(timeSpan);
    }

    /// <summary>
    /// 時刻を指定された日時にリセットします
    /// </summary>
    /// <param name="dateTime">新しい基準日時</param>
    public void Reset(DateTime dateTime)
    {
        _baseDateTime = dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : dateTime.ToUniversalTime();
        _offset = TimeSpan.Zero;
    }

    /// <summary>
    /// オフセットをリセットします
    /// </summary>
    public void ResetOffset()
    {
        _offset = TimeSpan.Zero;
    }
}

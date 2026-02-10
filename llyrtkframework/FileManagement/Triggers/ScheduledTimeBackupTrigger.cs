using llyrtkframework.FileManagement.Core;
using llyrtkframework.Results;
using llyrtkframework.Time;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// 特定時刻トリガー（例: 毎日13:00、毎週月曜9:00）
/// </summary>
public class ScheduledTimeBackupTrigger : BackupTrigger
{
    private Timer? _timer;
    private readonly TimeSpan _dailyTime;
    private readonly DayOfWeek? _dayOfWeek;
    private readonly IDateTimeProvider _dateTimeProvider;

    public override BackupTriggerType Type => BackupTriggerType.ScheduledTime;

    /// <summary>
    /// 毎日指定時刻にバックアップ
    /// </summary>
    /// <param name="dailyTime">実行時刻</param>
    /// <param name="dateTimeProvider">日時プロバイダ（テスト用）</param>
    public ScheduledTimeBackupTrigger(TimeSpan dailyTime, IDateTimeProvider? dateTimeProvider = null)
    {
        _dailyTime = dailyTime;
        _dateTimeProvider = dateTimeProvider ?? new SystemDateTimeProvider();
    }

    /// <summary>
    /// 毎週特定曜日の指定時刻にバックアップ
    /// </summary>
    /// <param name="dayOfWeek">曜日</param>
    /// <param name="time">実行時刻</param>
    /// <param name="dateTimeProvider">日時プロバイダ（テスト用）</param>
    public ScheduledTimeBackupTrigger(DayOfWeek dayOfWeek, TimeSpan time, IDateTimeProvider? dateTimeProvider = null)
    {
        _dailyTime = time;
        _dayOfWeek = dayOfWeek;
        _dateTimeProvider = dateTimeProvider ?? new SystemDateTimeProvider();
    }

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive)
            return;

        var nextRun = CalculateNextRunTime();
        var delay = nextRun - _dateTimeProvider.Now;

        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        _timer = new Timer(
            async _ =>
            {
                try
                {
                    await backupAction();
                }
                catch
                {
                    // ログはbackupAction内で処理済み
                }

                // 次回実行時刻を再計算
                var next = CalculateNextRunTime();
                var nextDelay = next - _dateTimeProvider.Now;

                if (nextDelay < TimeSpan.Zero)
                    nextDelay = TimeSpan.Zero;

                _timer?.Change(nextDelay, Timeout.InfiniteTimeSpan);
            },
            null,
            delay,
            Timeout.InfiniteTimeSpan
        );

        IsActive = true;
    }

    private DateTime CalculateNextRunTime()
    {
        var now = _dateTimeProvider.Now;
        var today = now.Date + _dailyTime;

        if (_dayOfWeek.HasValue)
        {
            // 週次スケジュール
            var daysUntilTarget = ((int)_dayOfWeek.Value - (int)now.DayOfWeek + 7) % 7;
            var targetDate = now.Date.AddDays(daysUntilTarget) + _dailyTime;

            if (targetDate <= now)
                targetDate = targetDate.AddDays(7);

            return targetDate;
        }
        else
        {
            // 日次スケジュール
            if (today <= now)
                today = today.AddDays(1);

            return today;
        }
    }

    public override void Unregister()
    {
        _timer?.Dispose();
        _timer = null;
        IsActive = false;
    }
}

using llyrtkframework.FileManagement.Core;
using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// 時間間隔トリガー（例: 30分ごと）
/// </summary>
public class IntervalBackupTrigger : BackupTrigger
{
    private Timer? _timer;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _startDelay;

    public override BackupTriggerType Type => BackupTriggerType.Interval;

    /// <summary>
    /// 時間間隔トリガーを作成します
    /// </summary>
    /// <param name="interval">バックアップ間隔</param>
    /// <param name="startDelay">最初のバックアップまでの遅延（nullの場合はintervalと同じ）</param>
    public IntervalBackupTrigger(TimeSpan interval, TimeSpan? startDelay = null)
    {
        _interval = interval;
        _startDelay = startDelay ?? interval;
    }

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive)
            return;

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
            },
            null,
            _startDelay,
            _interval
        );

        IsActive = true;
    }

    public override void Unregister()
    {
        _timer?.Dispose();
        _timer = null;
        IsActive = false;
    }
}

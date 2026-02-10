using llyrtkframework.FileManagement.Core;
using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// アプリ起動時トリガー
/// </summary>
public class OnStartupBackupTrigger : BackupTrigger
{
    private bool _hasExecuted;
    private readonly TimeSpan _delay;

    public override BackupTriggerType Type => BackupTriggerType.OnStartup;

    /// <summary>
    /// アプリ起動時トリガーを作成します
    /// </summary>
    /// <param name="delay">起動後の待機時間（nullの場合は5秒）</param>
    public OnStartupBackupTrigger(TimeSpan? delay = null)
    {
        _delay = delay ?? TimeSpan.FromSeconds(5);
    }

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive || _hasExecuted)
            return;

        // 起動時に1回だけ実行
        Task.Run(async () =>
        {
            await Task.Delay(_delay);

            try
            {
                await backupAction();
            }
            catch
            {
                // ログはbackupAction内で処理済み
            }

            _hasExecuted = true;
        });

        IsActive = true;
    }

    public override void Unregister()
    {
        IsActive = false;
    }
}

using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.Results;
using System.Reactive.Linq;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// ファイル変更後一定時間経過トリガー（デバウンス）
/// </summary>
public class OnModifiedBackupTrigger : BackupTrigger
{
    private IDisposable? _subscription;
    private readonly EventAggregator _eventAggregator;
    private readonly string _filePath;
    private readonly TimeSpan _debounceTime;

    public override BackupTriggerType Type => BackupTriggerType.OnModified;

    /// <summary>
    /// ファイル変更後一定時間経過トリガーを作成します
    /// </summary>
    /// <param name="eventAggregator">イベントアグリゲーター</param>
    /// <param name="filePath">監視するファイルパス</param>
    /// <param name="debounceTime">変更後の待機時間（nullの場合は30秒）</param>
    public OnModifiedBackupTrigger(
        EventAggregator eventAggregator,
        string filePath,
        TimeSpan? debounceTime = null)
    {
        _eventAggregator = eventAggregator;
        _filePath = Path.GetFullPath(filePath);
        _debounceTime = debounceTime ?? TimeSpan.FromSeconds(30);
    }

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive)
            return;

        _subscription = _eventAggregator
            .GetEvent<FileSavedEvent>()
            .Where(e => Path.GetFullPath(e.FilePath) == _filePath)
            .Throttle(_debounceTime) // 連続した変更をまとめる
            .Subscribe(async _ =>
            {
                try
                {
                    await backupAction();
                }
                catch
                {
                    // ログはbackupAction内で処理済み
                }
            });

        IsActive = true;
    }

    public override void Unregister()
    {
        _subscription?.Dispose();
        _subscription = null;
        IsActive = false;
    }
}

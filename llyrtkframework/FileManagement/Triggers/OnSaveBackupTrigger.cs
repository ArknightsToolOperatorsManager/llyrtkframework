using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.Results;
using System.Reactive.Linq;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// ファイル保存時トリガー
/// </summary>
public class OnSaveBackupTrigger : BackupTrigger
{
    private IDisposable? _subscription;
    private readonly EventAggregator _eventAggregator;
    private readonly string _filePath;

    public override BackupTriggerType Type => BackupTriggerType.OnSave;

    /// <summary>
    /// ファイル保存時トリガーを作成します
    /// </summary>
    /// <param name="eventAggregator">イベントアグリゲーター</param>
    /// <param name="filePath">監視するファイルパス</param>
    public OnSaveBackupTrigger(EventAggregator eventAggregator, string filePath)
    {
        _eventAggregator = eventAggregator;
        _filePath = Path.GetFullPath(filePath);
    }

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive)
            return;

        _subscription = _eventAggregator
            .GetEvent<FileSavedEvent>()
            .Where(e => Path.GetFullPath(e.FilePath) == _filePath)
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

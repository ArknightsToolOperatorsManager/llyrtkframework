using llyrtkframework.FileManagement.Core;
using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// 複数トリガーの組み合わせ
/// </summary>
public class CombinedBackupTrigger : BackupTrigger
{
    private readonly List<BackupTrigger> _triggers;

    public override BackupTriggerType Type => BackupTriggerType.Combined;

    /// <summary>
    /// 複数トリガーを組み合わせます
    /// </summary>
    /// <param name="triggers">組み合わせるトリガー</param>
    public CombinedBackupTrigger(params BackupTrigger[] triggers)
    {
        _triggers = triggers.ToList();
    }

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive)
            return;

        foreach (var trigger in _triggers)
        {
            trigger.Register(fileManager, backupAction);
        }

        IsActive = true;
    }

    public override void Unregister()
    {
        foreach (var trigger in _triggers)
        {
            trigger.Unregister();
        }

        IsActive = false;
    }

    public override void Dispose()
    {
        foreach (var trigger in _triggers)
        {
            trigger.Dispose();
        }

        base.Dispose();
    }
}

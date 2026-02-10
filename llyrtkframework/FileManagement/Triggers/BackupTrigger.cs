using llyrtkframework.FileManagement.Core;
using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// バックアップトリガーの種類
/// </summary>
public enum BackupTriggerType
{
    /// <summary>時間間隔（例: 30分ごと）</summary>
    Interval,

    /// <summary>特定時刻（例: 毎日13:00）</summary>
    ScheduledTime,

    /// <summary>ファイル保存時</summary>
    OnSave,

    /// <summary>ファイル変更時（保存後一定時間経過）</summary>
    OnModified,

    /// <summary>アプリ起動時</summary>
    OnStartup,

    /// <summary>アプリ終了時</summary>
    OnShutdown,

    /// <summary>手動のみ</summary>
    Manual,

    /// <summary>複数トリガーの組み合わせ</summary>
    Combined,

    /// <summary>GitHub同期トリガー</summary>
    GitHubSync
}

/// <summary>
/// バックアップトリガーの抽象基底クラス
/// </summary>
public abstract class BackupTrigger : IDisposable
{
    /// <summary>トリガーの種類</summary>
    public abstract BackupTriggerType Type { get; }

    /// <summary>トリガーが有効かどうか</summary>
    public bool IsActive { get; protected set; }

    /// <summary>
    /// トリガーを登録します（マネージャから呼ばれる）
    /// </summary>
    /// <param name="fileManager">対象のファイルマネージャ</param>
    /// <param name="backupAction">バックアップ実行アクション</param>
    public abstract void Register(IFileManager fileManager, Func<Task<Result>> backupAction);

    /// <summary>
    /// トリガーを解除します
    /// </summary>
    public abstract void Unregister();

    /// <summary>
    /// リソースを解放します
    /// </summary>
    public virtual void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }
}

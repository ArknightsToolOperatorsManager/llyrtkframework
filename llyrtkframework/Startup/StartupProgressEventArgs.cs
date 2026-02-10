namespace llyrtkframework.Startup;

/// <summary>
/// 起動タスクの進捗情報を表すイベント引数
/// </summary>
public class StartupProgressEventArgs : EventArgs
{
    /// <summary>
    /// 現在実行中のタスク名
    /// </summary>
    public string TaskName { get; }

    /// <summary>
    /// 現在のタスク番号（1から始まる）
    /// </summary>
    public int CurrentTask { get; }

    /// <summary>
    /// 全タスク数
    /// </summary>
    public int TotalTasks { get; }

    /// <summary>
    /// 進捗率（0.0～1.0）
    /// </summary>
    public double Progress => TotalTasks > 0 ? (double)CurrentTask / TotalTasks : 0.0;

    /// <summary>
    /// 進捗率（パーセント表示用、0～100）
    /// </summary>
    public int ProgressPercentage => (int)(Progress * 100);

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="taskName">タスク名</param>
    /// <param name="currentTask">現在のタスク番号（1から始まる）</param>
    /// <param name="totalTasks">全タスク数</param>
    public StartupProgressEventArgs(string taskName, int currentTask, int totalTasks)
    {
        TaskName = taskName ?? throw new ArgumentNullException(nameof(taskName));
        CurrentTask = currentTask;
        TotalTasks = totalTasks;
    }

    /// <summary>
    /// 文字列表現を返します
    /// </summary>
    public override string ToString()
    {
        return $"{TaskName} ({CurrentTask}/{TotalTasks}, {ProgressPercentage}%)";
    }
}

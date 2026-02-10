using llyrtkframework.Results;
using Microsoft.Extensions.Logging;

namespace llyrtkframework.Startup;

/// <summary>
/// 起動タスクを順次実行するランナー
/// </summary>
/// <remarks>
/// 複数の IStartupTask を Order プロパティでソートして順次実行し、
/// 進捗状況をイベントで通知します。
/// </remarks>
public class StartupTaskRunner
{
    private readonly IEnumerable<IStartupTask> _tasks;
    private readonly ILogger<StartupTaskRunner>? _logger;

    /// <summary>
    /// 進捗変更イベント
    /// </summary>
    /// <remarks>
    /// 各タスクの実行開始時に発行されます。
    /// </remarks>
    public event EventHandler<StartupProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="tasks">実行するタスクのコレクション</param>
    /// <param name="logger">ロガー（オプション）</param>
    public StartupTaskRunner(IEnumerable<IStartupTask> tasks, ILogger<StartupTaskRunner>? logger = null)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _logger = logger;
    }

    /// <summary>
    /// すべてのタスクを順次実行します
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>実行結果（失敗した場合は最初のエラー）</returns>
    /// <remarks>
    /// タスクは Order プロパティの昇順で実行されます。
    /// いずれかのタスクが失敗した場合、その時点で実行を中断し、失敗結果を返します。
    /// </remarks>
    public async Task<Result> RunAllAsync(CancellationToken cancellationToken = default)
    {
        var orderedTasks = _tasks.OrderBy(t => t.Order).ToList();
        var totalTasks = orderedTasks.Count;

        _logger?.LogInformation("Starting {TaskCount} startup tasks", totalTasks);

        for (int i = 0; i < orderedTasks.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning("Startup tasks cancelled at task {Index}/{Total}", i + 1, totalTasks);
                return Result.Failure("Startup tasks were cancelled");
            }

            var task = orderedTasks[i];
            var currentTaskNumber = i + 1;

            _logger?.LogInformation(
                "Executing startup task {Index}/{Total}: {TaskName} (Order: {Order})",
                currentTaskNumber,
                totalTasks,
                task.Name,
                task.Order
            );

            // 進捗イベントを発行
            OnProgressChanged(new StartupProgressEventArgs(
                task.Name,
                currentTaskNumber,
                totalTasks
            ));

            try
            {
                var result = await task.ExecuteAsync(cancellationToken);

                if (result.IsFailure)
                {
                    _logger?.LogError(
                        "Startup task {TaskName} failed: {Error}",
                        task.Name,
                        result.ErrorMessage
                    );
                    return Result.Failure($"Startup task '{task.Name}' failed: {result.ErrorMessage}");
                }

                _logger?.LogInformation(
                    "Startup task {TaskName} completed successfully",
                    task.Name
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Startup task {TaskName} threw an exception",
                    task.Name
                );
                return Result.FromException(ex, $"Startup task '{task.Name}' threw an exception");
            }
        }

        _logger?.LogInformation("All {TaskCount} startup tasks completed successfully", totalTasks);
        return Result.Success();
    }

    /// <summary>
    /// 進捗変更イベントを発行します
    /// </summary>
    /// <param name="e">イベント引数</param>
    protected virtual void OnProgressChanged(StartupProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }
}

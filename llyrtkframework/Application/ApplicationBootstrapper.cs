using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace llyrtkframework.Application;

/// <summary>
/// アプリケーションの起動フローを管理するブートストラッパー
/// 3フェーズ起動: Pre-boot → Core Init → UI Setup
/// </summary>
public class ApplicationBootstrapper
{
    private readonly ILogger<ApplicationBootstrapper> _logger;
    private readonly List<IPreBootTask> _preBootTasks = new();
    private readonly List<ICoreInitTask> _coreInitTasks = new();
    private readonly List<IUiSetupTask> _uiSetupTasks = new();

    public ApplicationBootstrapper(ILogger<ApplicationBootstrapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Pre-bootタスクを追加
    /// </summary>
    public ApplicationBootstrapper AddPreBootTask(IPreBootTask task)
    {
        _preBootTasks.Add(task);
        return this;
    }

    /// <summary>
    /// Core Initタスクを追加
    /// </summary>
    public ApplicationBootstrapper AddCoreInitTask(ICoreInitTask task)
    {
        _coreInitTasks.Add(task);
        return this;
    }

    /// <summary>
    /// UI Setupタスクを追加
    /// </summary>
    public ApplicationBootstrapper AddUiSetupTask(IUiSetupTask task)
    {
        _uiSetupTasks.Add(task);
        return this;
    }

    /// <summary>
    /// アプリケーション起動を実行
    /// </summary>
    public async Task<Result> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Application bootstrap started");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Phase 1: Pre-boot
            var preBootResult = await ExecutePreBootAsync(cancellationToken);
            if (preBootResult.IsFailure)
            {
                _logger.LogError("Pre-boot phase failed: {Error}", preBootResult.ErrorMessage);
                return preBootResult;
            }

            // Phase 2: Core Init
            var coreInitResult = await ExecuteCoreInitAsync(cancellationToken);
            if (coreInitResult.IsFailure)
            {
                _logger.LogError("Core init phase failed: {Error}", coreInitResult.ErrorMessage);
                return coreInitResult;
            }

            // Phase 3: UI Setup
            var uiSetupResult = await ExecuteUiSetupAsync(cancellationToken);
            if (uiSetupResult.IsFailure)
            {
                _logger.LogError("UI setup phase failed: {Error}", uiSetupResult.ErrorMessage);
                return uiSetupResult;
            }

            stopwatch.Stop();
            _logger.LogInformation("Application bootstrap completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application bootstrap failed with exception");
            return Result.FromException(ex, "Application bootstrap failed");
        }
    }

    private async Task<Result> ExecutePreBootAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing pre-boot phase ({Count} tasks)", _preBootTasks.Count);

        foreach (var task in _preBootTasks)
        {
            var taskName = task.GetType().Name;
            _logger.LogDebug("Executing pre-boot task: {TaskName}", taskName);

            var result = await task.ExecuteAsync(cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError("Pre-boot task {TaskName} failed: {Error}", taskName, result.ErrorMessage);
                return result;
            }

            _logger.LogDebug("Pre-boot task {TaskName} completed", taskName);
        }

        _logger.LogInformation("Pre-boot phase completed");
        return Result.Success();
    }

    private async Task<Result> ExecuteCoreInitAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing core init phase ({Count} tasks)", _coreInitTasks.Count);

        foreach (var task in _coreInitTasks)
        {
            var taskName = task.GetType().Name;
            _logger.LogDebug("Executing core init task: {TaskName}", taskName);

            var result = await task.ExecuteAsync(cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError("Core init task {TaskName} failed: {Error}", taskName, result.ErrorMessage);
                return result;
            }

            _logger.LogDebug("Core init task {TaskName} completed", taskName);
        }

        _logger.LogInformation("Core init phase completed");
        return Result.Success();
    }

    private async Task<Result> ExecuteUiSetupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing UI setup phase ({Count} tasks)", _uiSetupTasks.Count);

        foreach (var task in _uiSetupTasks)
        {
            var taskName = task.GetType().Name;
            _logger.LogDebug("Executing UI setup task: {TaskName}", taskName);

            var result = await task.ExecuteAsync(cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError("UI setup task {TaskName} failed: {Error}", taskName, result.ErrorMessage);
                return result;
            }

            _logger.LogDebug("UI setup task {TaskName} completed", taskName);
        }

        _logger.LogInformation("UI setup phase completed");
        return Result.Success();
    }
}

/// <summary>
/// Pre-bootフェーズのタスク
/// クラッシュ検出、バージョン確認、Mutex確認など
/// </summary>
public interface IPreBootTask
{
    Task<Result> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Core Initフェーズのタスク
/// DI登録、設定読み込み、データベース初期化など
/// </summary>
public interface ICoreInitTask
{
    Task<Result> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// UI Setupフェーズのタスク
/// GitHub同期、更新チェック、初回起動処理など
/// </summary>
public interface IUiSetupTask
{
    Task<Result> ExecuteAsync(CancellationToken cancellationToken = default);
}

using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Reactive.Subjects;

namespace llyrtkframework.Application;

/// <summary>
/// アプリケーションのライフサイクル管理
/// </summary>
public class ApplicationLifecycleManager : IDisposable
{
    private readonly ILogger<ApplicationLifecycleManager> _logger;
    private readonly Subject<ApplicationState> _stateChanged = new();
    private ApplicationState _currentState = ApplicationState.NotStarted;
    private readonly List<IApplicationShutdownHandler> _shutdownHandlers = new();
    private readonly CrashRecoveryManager? _crashRecoveryManager;

    public ApplicationLifecycleManager(
        ILogger<ApplicationLifecycleManager> logger,
        CrashRecoveryManager? crashRecoveryManager = null)
    {
        _logger = logger;
        _crashRecoveryManager = crashRecoveryManager;
    }

    /// <summary>
    /// 現在のアプリケーション状態
    /// </summary>
    public ApplicationState CurrentState => _currentState;

    /// <summary>
    /// 状態変更を監視
    /// </summary>
    public IObservable<ApplicationState> StateChanged => _stateChanged;

    /// <summary>
    /// シャットダウンハンドラーを追加
    /// </summary>
    public void RegisterShutdownHandler(IApplicationShutdownHandler handler)
    {
        _shutdownHandlers.Add(handler);
        _logger.LogDebug("Shutdown handler registered: {HandlerType}", handler.GetType().Name);
    }

    /// <summary>
    /// 状態を変更
    /// </summary>
    public void SetState(ApplicationState newState)
    {
        if (_currentState == newState)
            return;

        var previousState = _currentState;
        _currentState = newState;

        _logger.LogInformation("Application state changed: {PreviousState} -> {NewState}", previousState, newState);
        _stateChanged.OnNext(newState);
    }

    /// <summary>
    /// アプリケーションをシャットダウン
    /// </summary>
    public async Task<Result> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Application shutdown initiated");
        SetState(ApplicationState.ShuttingDown);

        try
        {
            // すべてのシャットダウンハンドラーを実行
            foreach (var handler in _shutdownHandlers)
            {
                var handlerName = handler.GetType().Name;
                _logger.LogDebug("Executing shutdown handler: {HandlerName}", handlerName);

                try
                {
                    var result = await handler.OnShutdownAsync(cancellationToken);
                    if (result.IsFailure)
                    {
                        _logger.LogWarning("Shutdown handler {HandlerName} failed: {Error}", handlerName, result.ErrorMessage);
                        // エラーがあっても続行
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Shutdown handler {HandlerName} threw exception", handlerName);
                    // 例外があっても続行
                }
            }

            // クラッシュフラグをクリア（正常終了）
            if (_crashRecoveryManager != null)
            {
                var clearResult = _crashRecoveryManager.ClearCrashFlag();
                if (clearResult.IsFailure)
                {
                    _logger.LogWarning("Failed to clear crash flag: {Error}", clearResult.ErrorMessage);
                }
            }

            SetState(ApplicationState.Stopped);
            _logger.LogInformation("Application shutdown completed");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application shutdown failed");
            SetState(ApplicationState.Error);
            return Result.FromException(ex, "Application shutdown failed");
        }
    }

    public void Dispose()
    {
        _stateChanged.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// アプリケーション状態
/// </summary>
public enum ApplicationState
{
    /// <summary>未起動</summary>
    NotStarted,

    /// <summary>初期化中</summary>
    Initializing,

    /// <summary>実行中</summary>
    Running,

    /// <summary>シャットダウン中</summary>
    ShuttingDown,

    /// <summary>停止</summary>
    Stopped,

    /// <summary>エラー</summary>
    Error
}

/// <summary>
/// シャットダウンハンドラー
/// </summary>
public interface IApplicationShutdownHandler
{
    /// <summary>
    /// シャットダウン時に呼ばれる
    /// </summary>
    Task<Result> OnShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// シャットダウンハンドラー: ファイル保存
/// </summary>
public class SaveFilesShutdownHandler : IApplicationShutdownHandler
{
    private readonly Func<CancellationToken, Task<Result>> _saveAction;
    private readonly ILogger<SaveFilesShutdownHandler> _logger;

    public SaveFilesShutdownHandler(
        Func<CancellationToken, Task<Result>> saveAction,
        ILogger<SaveFilesShutdownHandler> logger)
    {
        _saveAction = saveAction;
        _logger = logger;
    }

    public async Task<Result> OnShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving files on shutdown");
        return await _saveAction(cancellationToken);
    }
}

/// <summary>
/// シャットダウンハンドラー: 接続クローズ
/// </summary>
public class CloseConnectionsShutdownHandler : IApplicationShutdownHandler
{
    private readonly Func<CancellationToken, Task<Result>> _closeAction;
    private readonly ILogger<CloseConnectionsShutdownHandler> _logger;

    public CloseConnectionsShutdownHandler(
        Func<CancellationToken, Task<Result>> closeAction,
        ILogger<CloseConnectionsShutdownHandler> logger)
    {
        _closeAction = closeAction;
        _logger = logger;
    }

    public async Task<Result> OnShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Closing connections on shutdown");
        return await _closeAction(cancellationToken);
    }
}

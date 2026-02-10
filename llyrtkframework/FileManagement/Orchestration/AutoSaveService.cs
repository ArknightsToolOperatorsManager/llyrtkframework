using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Orchestration;

/// <summary>
/// 自動保存サービス（ポーリング + 実行）
/// </summary>
public sealed class AutoSaveService : IDisposable
{
    private readonly FileManagerRegistry _registry;
    private readonly ILogger<AutoSaveService> _logger;
    private readonly EventAggregator? _eventAggregator;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private System.Threading.Timer? _timer;
    private TimeSpan _interval = TimeSpan.FromMilliseconds(500);
    private bool _isRunning;
    private bool _disposed;

    public AutoSaveService(
        FileManagerRegistry registry,
        ILogger<AutoSaveService> logger,
        EventAggregator? eventAggregator = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventAggregator = eventAggregator;
    }

    /// <summary>
    /// 自動保存が実行中かどうか
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 自動保存を開始
    /// </summary>
    /// <param name="interval">自動保存のポーリング間隔（デフォルト: 500ms）</param>
    public void Start(TimeSpan? interval = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutoSaveService));

        if (_isRunning)
        {
            _logger.LogWarning("Auto-save is already running");
            return;
        }

        _interval = interval ?? TimeSpan.FromMilliseconds(500);
        _isRunning = true;

        _timer = new System.Threading.Timer(
            async _ => await AutoSaveTickAsync(),
            null,
            _interval,
            _interval);

        _logger.LogInformation("Auto-save started with interval: {Interval}ms", _interval.TotalMilliseconds);
    }

    /// <summary>
    /// 自動保存を停止
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Auto-save is not running");
            return;
        }

        _isRunning = false;
        _timer?.Dispose();
        _timer = null;

        _logger.LogInformation("Auto-save stopped");
    }

    /// <summary>
    /// 即座に自動保存を実行（手動トリガー）
    /// </summary>
    public async Task<Result> ExecuteNowAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutoSaveService));

        return await AutoSaveTickAsync(cancellationToken);
    }

    /// <summary>
    /// 自動保存のティック処理
    /// </summary>
    private async Task<Result> AutoSaveTickAsync(CancellationToken cancellationToken = default)
    {
        // 同時実行を防止
        if (!await _lock.WaitAsync(0, cancellationToken))
        {
            _logger.LogTrace("Auto-save tick skipped (previous save still running)");
            return Result.Success();
        }

        try
        {
            // HasPendingAutoSave フラグがONのファイルマネージャーを取得
            var managers = _registry.GetAllManagers()
                .Where(reg => reg.Manager.HasPendingAutoSave)
                .Select(reg => reg.Manager)
                .ToList();

            if (!managers.Any())
            {
                _logger.LogTrace("Auto-save tick: No pending auto-save");
                return Result.Success();
            }

            _logger.LogDebug("Auto-save tick: {Count} files with pending auto-save", managers.Count);

            _eventAggregator?.Publish(new AutoSaveStartedEvent
            {
                FileCount = managers.Count,
                StartedAt = DateTime.Now
            });

            // 各ファイルマネージャーの AutoSaveAsync を呼び出す
            var tasks = managers
                .Select(async manager =>
                {
                    try
                    {
                        var result = await manager.AutoSaveAsync(cancellationToken);

                        if (result.IsSuccess)
                        {
                            manager.ClearAutoSaveFlag();

                            _eventAggregator?.Publish(new AutoSaveCompletedEvent
                            {
                                FilePath = manager.FilePath,
                                SavedAt = DateTime.Now
                            });

                            _logger.LogDebug("Auto-save completed: {FilePath}", manager.FilePath);
                        }
                        else
                        {
                            _eventAggregator?.Publish(new AutoSaveFailedEvent
                            {
                                FilePath = manager.FilePath,
                                ErrorMessage = result.ErrorMessage ?? "Unknown error",
                                FailedAt = DateTime.Now
                            });

                            _logger.LogWarning("Auto-save failed for {FilePath}: {Error}",
                                manager.FilePath, result.ErrorMessage);
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Auto-save exception for {FilePath}", manager.FilePath);

                        _eventAggregator?.Publish(new AutoSaveFailedEvent
                        {
                            FilePath = manager.FilePath,
                            ErrorMessage = ex.Message,
                            FailedAt = DateTime.Now
                        });

                        return Result.FromException(ex, $"Auto-save failed: {manager.FilePath}");
                    }
                })
                .ToList();

            var results = await Task.WhenAll(tasks);
            return Result.Combine(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-save tick exception");
            return Result.FromException(ex, "Auto-save tick failed");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _lock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

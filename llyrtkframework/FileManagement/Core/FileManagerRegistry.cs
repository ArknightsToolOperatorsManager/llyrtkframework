using llyrtkframework.FileManagement.Events;
using llyrtkframework.FileManagement.GitHub;
using llyrtkframework.FileManagement.Triggers;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Core;

/// <summary>
/// ファイルマネージャーの登録管理クラス（Singleton）
/// </summary>
public class FileManagerRegistry : IDisposable
{
    private static readonly Lazy<FileManagerRegistry> _instance =
        new(() => new FileManagerRegistry());

    public static FileManagerRegistry Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, FileManagerRegistration> _registrations = new();
    private ILogger<FileManagerRegistry>? _logger;
    private EventAggregator? _eventAggregator;
    private System.Threading.Timer? _autoSaveTimer;
    private bool _isAutoSaveEnabled = false;
    private TimeSpan _autoSaveInterval = TimeSpan.FromMilliseconds(500);
    private readonly SemaphoreSlim _autoSaveLock = new(1, 1);

    public class FileManagerRegistration
    {
        public required IFileManager Manager { get; init; }
        public required List<BackupTrigger> Triggers { get; init; }
        public GitHubFileSync? GitHubSync { get; init; }
    }

    private FileManagerRegistry()
    {
        // 必要に応じてDIコンテナから取得
        // ここではシンプルにnull許容
        _logger = null;
        _eventAggregator = null;
    }

    /// <summary>
    /// ロガーとイベントアグリゲーターを設定します（アプリ起動時に呼び出し）
    /// </summary>
    public void Initialize(ILogger<FileManagerRegistry>? logger = null, EventAggregator? eventAggregator = null)
    {
        _logger = logger;
        _eventAggregator = eventAggregator;
        _logger?.LogDebug("FileManagerRegistry initialized");
    }

    /// <summary>
    /// 自動保存を開始します（500msポーリング）
    /// </summary>
    /// <param name="interval">自動保存のポーリング間隔（デフォルト: 500ms）</param>
    public void StartAutoSave(TimeSpan? interval = null)
    {
        if (_isAutoSaveEnabled)
        {
            _logger?.LogWarning("Auto-save is already enabled");
            return;
        }

        _autoSaveInterval = interval ?? TimeSpan.FromMilliseconds(500);
        _isAutoSaveEnabled = true;

        _autoSaveTimer = new System.Threading.Timer(
            async _ => await AutoSaveTickAsync(),
            null,
            _autoSaveInterval,
            _autoSaveInterval);

        _logger?.LogInformation("Auto-save started with interval: {Interval}ms", _autoSaveInterval.TotalMilliseconds);
    }

    /// <summary>
    /// 自動保存を停止します
    /// </summary>
    public void StopAutoSave()
    {
        if (!_isAutoSaveEnabled)
        {
            _logger?.LogWarning("Auto-save is already disabled");
            return;
        }

        _isAutoSaveEnabled = false;
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;

        _logger?.LogInformation("Auto-save stopped");
    }

    /// <summary>
    /// 自動保存のティック処理
    /// </summary>
    private async Task AutoSaveTickAsync()
    {
        // 同時実行を防止
        if (!await _autoSaveLock.WaitAsync(0))
        {
            _logger?.LogTrace("Auto-save tick skipped (previous save still running)");
            return;
        }

        try
        {
            // HasPendingAutoSave フラグがONのファイルマネージャーを取得
            var unsavedManagers = _registrations.Values
                .Where(reg => reg.Manager.HasPendingAutoSave)
                .Select(reg => reg.Manager)
                .ToList();

            if (!unsavedManagers.Any())
            {
                _logger?.LogTrace("Auto-save tick: No unsaved changes");
                return;
            }

            _logger?.LogDebug("Auto-save tick: {Count} files with unsaved changes", unsavedManagers.Count);

            _eventAggregator?.Publish(new AutoSaveStartedEvent
            {
                FileCount = unsavedManagers.Count,
                StartedAt = DateTime.Now
            });

            // 各ファイルマネージャーの AutoSaveAsync を呼び出す
            var tasks = unsavedManagers
                .Select(async manager =>
                {
                    try
                    {
                        // AutoSaveAsync を呼び出し（AutoSavableFileManager の場合は保存実行）
                        var result = await manager.AutoSaveAsync();

                        if (result.IsSuccess)
                        {
                            manager.ClearAutoSaveFlag();

                            _eventAggregator?.Publish(new AutoSaveCompletedEvent
                            {
                                FilePath = manager.FilePath,
                                SavedAt = DateTime.Now
                            });

                            _logger?.LogDebug("Auto-save completed: {FilePath}", manager.FilePath);
                        }
                        else
                        {
                            _eventAggregator?.Publish(new AutoSaveFailedEvent
                            {
                                FilePath = manager.FilePath,
                                ErrorMessage = result.ErrorMessage ?? "Unknown error",
                                FailedAt = DateTime.Now
                            });

                            _logger?.LogWarning("Auto-save failed for {FilePath}: {Error}",
                                manager.FilePath, result.ErrorMessage);
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Auto-save exception for {FilePath}", manager.FilePath);

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

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Auto-save tick exception");
        }
        finally
        {
            _autoSaveLock.Release();
        }
    }

    /// <summary>
    /// 自動保存が有効かどうか
    /// </summary>
    public bool IsAutoSaveEnabled => _isAutoSaveEnabled;

    /// <summary>
    /// ファイルマネージャーを登録し、トリガーを設定します
    /// </summary>
    public void Register<T>(IFileManager<T> manager, List<BackupTrigger> triggers, GitHubFileSync? gitHubSync = null) where T : class
    {
        var key = manager.FilePath;

        var registration = new FileManagerRegistration
        {
            Manager = manager,
            Triggers = triggers,
            GitHubSync = gitHubSync
        };

        if (_registrations.TryAdd(key, registration))
        {
            // トリガーを登録
            foreach (var trigger in triggers)
            {
                trigger.Register(manager, async () => await manager.CreateBackupAsync());
            }

            _logger?.LogInformation(
                "Registered file manager: {FilePath} with {TriggerCount} triggers, GitHub sync: {GitHubEnabled}",
                key,
                triggers.Count,
                gitHubSync != null
            );
        }
        else
        {
            _logger?.LogWarning("File manager already registered: {FilePath}", key);
        }
    }

    /// <summary>
    /// ファイルマネージャーを登録解除します
    /// </summary>
    public void Unregister(string filePath)
    {
        var key = Path.GetFullPath(filePath);

        if (_registrations.TryRemove(key, out var registration))
        {
            // トリガーを解除
            foreach (var trigger in registration.Triggers)
            {
                trigger.Unregister();
                trigger.Dispose();
            }

            _logger?.LogInformation("Unregistered file manager: {FilePath}", key);
        }
    }

    /// <summary>
    /// すべてのファイルのバックアップを実行します
    /// </summary>
    public async Task<Result> ExecuteBackupAllAsync()
    {
        _logger?.LogInformation("Starting backup for all files ({Count} files)", _registrations.Count);

        var tasks = _registrations.Values
            .Select(reg => reg.Manager.CreateBackupAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
            _logger?.LogError("Backup failed for some files: {Errors}", errorMessages);
            return Result.Failure($"Backup partially failed: {errorMessages}");
        }

        _logger?.LogInformation("Backup completed successfully for all files");
        _eventAggregator?.Publish(new GlobalBackupCompletedEvent(_registrations.Count));

        return Result.Success();
    }

    /// <summary>
    /// 特定のファイルのバックアップを実行します
    /// </summary>
    public async Task<Result> ExecuteBackupAsync(string filePath)
    {
        var key = Path.GetFullPath(filePath);

        if (_registrations.TryGetValue(key, out var registration))
        {
            return await registration.Manager.CreateBackupAsync();
        }

        return Result.Failure($"File manager not found: {filePath}");
    }

    /// <summary>
    /// 変更されたファイルのみバックアップ（差分バックアップ）
    /// </summary>
    public async Task<Result> ExecuteIncrementalBackupAsync()
    {
        var modifiedManagers = _registrations.Values
            .Where(reg => reg.Manager.HasChangesSinceBackup)
            .Select(reg => reg.Manager)
            .ToList();

        if (!modifiedManagers.Any())
        {
            _logger?.LogInformation("No modified files, skipping backup");
            return Result.Success();
        }

        _logger?.LogInformation("Starting incremental backup ({Count} files)", modifiedManagers.Count);

        var tasks = modifiedManagers
            .Select(manager => manager.CreateBackupAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);
        return Result.Combine(results);
    }

    /// <summary>
    /// 特定のファイルマネージャーを取得
    /// </summary>
    public IFileManager? GetManager(string filePath)
    {
        var key = Path.GetFullPath(filePath);
        return _registrations.TryGetValue(key, out var registration) ? registration.Manager : null;
    }

    /// <summary>
    /// すべてのファイルマネージャー登録を取得
    /// </summary>
    public IReadOnlyCollection<FileManagerRegistration> GetAllManagers()
    {
        return _registrations.Values.ToList();
    }

    /// <summary>
    /// 登録されているファイル一覧を取得します
    /// </summary>
    public IReadOnlyList<string> GetRegisteredFiles()
    {
        return _registrations.Keys.ToList();
    }

    /// <summary>
    /// 登録されているファイル数を取得します
    /// </summary>
    public int RegisteredFileCount => _registrations.Count;

    /// <summary>
    /// すべてのファイルをGitHubと同期します
    /// </summary>
    public async Task<Result> SyncAllWithGitHubAsync(CancellationToken cancellationToken = default)
    {
        var gitHubSyncs = _registrations.Values
            .Where(reg => reg.GitHubSync != null)
            .Select(reg => reg.GitHubSync!)
            .ToList();

        if (!gitHubSyncs.Any())
        {
            _logger?.LogInformation("No GitHub-enabled file managers registered");
            return Result.Success();
        }

        _logger?.LogInformation("Starting GitHub sync for all files ({Count} files)", gitHubSyncs.Count);

        var tasks = gitHubSyncs
            .Select(sync => sync.CheckAndSyncAsync(cancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
            _logger?.LogError("GitHub sync failed for some files: {Errors}", errorMessages);
            return Result.Failure($"GitHub sync partially failed: {errorMessages}");
        }

        _logger?.LogInformation("GitHub sync completed successfully for all files");
        return Result.Success();
    }

    /// <summary>
    /// 特定のファイルをGitHubと同期します
    /// </summary>
    public async Task<Result> SyncWithGitHubAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFullPath(filePath);

        if (!_registrations.TryGetValue(key, out var registration))
        {
            return Result.Failure($"File manager not found: {filePath}");
        }

        if (registration.GitHubSync == null)
        {
            return Result.Failure($"GitHub sync is not enabled for: {filePath}");
        }

        _logger?.LogInformation("Starting GitHub sync for: {FilePath}", filePath);

        var result = await registration.GitHubSync.CheckAndSyncAsync(cancellationToken);

        if (result.IsSuccess)
        {
            _logger?.LogInformation("GitHub sync completed for: {FilePath}", filePath);
        }
        else
        {
            _logger?.LogError("GitHub sync failed for {FilePath}: {Error}", filePath, result.ErrorMessage);
        }

        return result;
    }

    public void Dispose()
    {
        // 自動保存を停止
        StopAutoSave();

        foreach (var registration in _registrations.Values)
        {
            foreach (var trigger in registration.Triggers)
            {
                trigger.Unregister();
                trigger.Dispose();
            }
        }

        _registrations.Clear();
        _autoSaveLock.Dispose();
        GC.SuppressFinalize(this);
    }
}

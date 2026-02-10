using llyrtkframework.FileManagement.Backup;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.FileManagement.GitHub;
using llyrtkframework.FileManagement.Services;
using llyrtkframework.FileManagement.Triggers;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Core;

/// <summary>
/// ファイル管理の基底クラス
/// </summary>
/// <typeparam name="T">管理するデータの型</typeparam>
public abstract class FileManagerBase<T> : IFileManager<T> where T : class
{
    // サービス
    private readonly IFileIOService<T> _fileIOService;
    private readonly IBackupService _backupService;
    private readonly IGitHubSyncService<T> _gitHubSyncService;
    private readonly IAutoSaveService<T> _cacheService;

    // その他の依存
    private readonly IFileSerializer<T> _serializer;
    private readonly ILogger _logger;
    private readonly EventAggregator? _eventAggregator;
    private readonly List<BackupTrigger> _backupTriggers;
    private readonly GitHubClient? _gitHubClient;

    // 状態管理
    private readonly ReaderWriterLockSlim _stateLock = new();
    private bool _hasChangesSinceBackup;
    private bool _hasPendingAutoSave;
    private bool _isAutoSaveEnabled = false;

    /// <summary>
    /// ファイルパス（相対または絶対パス）
    /// 継承クラスでオーバーライドして設定します
    /// </summary>
    protected abstract string ConfigureFilePath();

    /// <summary>
    /// バックアップトリガーのリストを設定します
    /// 継承クラスでオーバーライドしてカスタマイズ可能（デフォルト: 空リスト）
    /// </summary>
    protected virtual List<BackupTrigger> ConfigureBackupTriggers() => new();

    /// <summary>
    /// バックアップオプションを設定します
    /// 継承クラスでオーバーライドしてカスタマイズ可能（デフォルト: デフォルト設定）
    /// </summary>
    protected virtual BackupOptions ConfigureBackupOptions() => new();

    /// <summary>
    /// GitHub同期オプションを設定します
    /// 継承クラスでオーバーライドしてGitHub同期を有効化（デフォルト: null = 無効）
    /// </summary>
    protected virtual GitHubFileOptions? ConfigureGitHubOptions() => null;

    /// <summary>
    /// 自動保存機能を有効にするかどうか
    /// 継承クラスでオーバーライドしてカスタマイズ可能（デフォルト: false）
    /// </summary>
    protected virtual bool ConfigureAutoSaveEnabled() => false;

    protected FileManagerBase(
        IFileSerializer<T> serializer,
        ILogger logger,
        EventAggregator? eventAggregator = null)
    {
        _serializer = serializer;
        _logger = logger;
        _eventAggregator = eventAggregator;

        // 継承クラスから設定を取得
        FilePath = Path.GetFullPath(ConfigureFilePath());
        _backupTriggers = ConfigureBackupTriggers();
        var backupOptions = ConfigureBackupOptions();
        var githubOptions = ConfigureGitHubOptions();
        _isAutoSaveEnabled = ConfigureAutoSaveEnabled();

        // サービスの初期化
        _fileIOService = new FileIOService<T>(serializer, logger, FilePath, eventAggregator);

        var backupManager = new BackupManager(FilePath, backupOptions, logger);
        _backupService = new BackupService(backupManager, logger, FilePath, eventAggregator);

        _cacheService = new CacheService<T>();

        // GitHub同期の初期化
        GitHubFileSync? gitHubFileSync = null;
        if (githubOptions != null)
        {
            _gitHubClient = new GitHubClient(logger);
            gitHubFileSync = new GitHubFileSync(
                githubOptions,
                _gitHubClient,
                this,
                logger,
                eventAggregator);

            _logger.LogDebug("GitHub sync enabled for: {FilePath}", FilePath);
        }

        _gitHubSyncService = new GitHubSyncService<T>(gitHubFileSync, _fileIOService, logger, FilePath);

        EnsureDirectoryExists();

        // FileManagerRegistryに登録
        FileManagerRegistry.Instance.Register(this, _backupTriggers, gitHubFileSync);

        _logger.LogDebug("FileManager created for: {FilePath}", FilePath);
    }

    public string FilePath { get; }

    public bool FileExists => File.Exists(FilePath);

    public bool IsGitHubSyncEnabled => _gitHubSyncService.IsEnabled;

    /// <summary>
    /// 最後のバックアップ以降に変更があるか
    /// </summary>
    public bool HasChangesSinceBackup
    {
        get
        {
            _stateLock.EnterReadLock();
            try { return _hasChangesSinceBackup; }
            finally { _stateLock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// 自動保存待ちのデータがあるか
    /// </summary>
    public bool HasPendingAutoSave
    {
        get
        {
            _stateLock.EnterReadLock();
            try { return _hasPendingAutoSave; }
            finally { _stateLock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// データが変更されたことをマーク（両方のフラグを立てる）
    /// </summary>
    public void MarkAsChanged()
    {
        _stateLock.EnterWriteLock();
        try
        {
            _hasPendingAutoSave = true;
            _hasChangesSinceBackup = true;
            _logger.LogTrace("Marked as changed: {FilePath}", FilePath);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 自動保存フラグのみクリア（自動保存完了後に呼び出し）
    /// </summary>
    public void ClearAutoSaveFlag()
    {
        _stateLock.EnterWriteLock();
        try
        {
            _hasPendingAutoSave = false;
            _logger.LogTrace("Cleared auto-save flag: {FilePath}", FilePath);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// バックアップフラグのみクリア（バックアップ完了後に呼び出し）
    /// </summary>
    public void ClearBackupFlag()
    {
        _stateLock.EnterWriteLock();
        try
        {
            _hasChangesSinceBackup = false;
            _logger.LogTrace("Cleared backup flag: {FilePath}", FilePath);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 自動保存機能の有効/無効を設定
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        _isAutoSaveEnabled = enabled;
        _logger.LogDebug("Auto-save {Status} for: {FilePath}", enabled ? "enabled" : "disabled", FilePath);
    }

    /// <summary>
    /// 自動保存が有効かどうか
    /// </summary>
    public bool IsAutoSaveEnabled => _isAutoSaveEnabled;

    public async Task<Result<T>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return await _fileIOService.LoadAsync(
            onSuccess: data =>
            {
                if (_isAutoSaveEnabled)
                {
                    _cacheService.UpdateCache(data);
                }
            },
            cancellationToken);
    }

    public Result<T> Load()
    {
        return _fileIOService.Load(
            onSuccess: data =>
            {
                if (_isAutoSaveEnabled)
                {
                    _cacheService.UpdateCache(data);
                }
            });
    }

    public async Task<Result> SaveAsync(T data, CancellationToken cancellationToken = default)
    {
        return await _fileIOService.SaveAsync(
            data,
            onSuccess: () =>
            {
                // 保存成功時の状態更新
                _stateLock.EnterWriteLock();
                try
                {
                    _hasPendingAutoSave = false;
                    _hasChangesSinceBackup = true;
                }
                finally
                {
                    _stateLock.ExitWriteLock();
                }

                // キャッシュ更新
                if (_isAutoSaveEnabled)
                {
                    _cacheService.UpdateCache(data);
                }
            },
            cancellationToken);
    }

    public Result Save(T data)
    {
        return _fileIOService.Save(
            data,
            onSuccess: () =>
            {
                // 保存成功時の状態更新
                _stateLock.EnterWriteLock();
                try
                {
                    _hasPendingAutoSave = false;
                    _hasChangesSinceBackup = true;
                }
                finally
                {
                    _stateLock.ExitWriteLock();
                }

                // キャッシュ更新
                if (_isAutoSaveEnabled)
                {
                    _cacheService.UpdateCache(data);
                }
            });
    }

    public async Task<Result> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!FileExists)
        {
            return Result.Failure("Cannot backup: file does not exist");
        }

        return await _backupService.CreateBackupAsync(
            onSuccess: _ =>
            {
                ClearBackupFlag();
            },
            cancellationToken);
    }

    public async Task<Result<T>> RestoreFromLatestBackupAsync()
    {
        var contentResult = await _backupService.GetLatestBackupContentAsync();

        if (contentResult.IsFailure)
        {
            return Result<T>.Failure(contentResult.ErrorMessage!);
        }

        try
        {
            var data = _serializer.Deserialize(contentResult.Value);
            _logger.LogInformation("Restored from latest backup: {FilePath}", FilePath);
            return Result<T>.Success(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize backup content for {FilePath}", FilePath);
            return Result<T>.FromException(ex, "Failed to deserialize backup content");
        }
    }

    public async Task<Result<T>> RestoreWithRollbackAsync(
        RollbackOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var contentResult = await _backupService.GetBackupContentWithRollbackAsync(
            options,
            onRollback: rollbackInfo =>
            {
                // ロールバック発生時のログ出力などの処理
                if (rollbackInfo.SuccessfulBackupPath != null)
                {
                    _logger.LogWarning("Rollback occurred. Restored from: {Path}", rollbackInfo.SuccessfulBackupPath);
                }
                else
                {
                    _logger.LogError("All rollback attempts failed");
                }
            },
            cancellationToken);

        if (contentResult.IsFailure)
        {
            return Result<T>.Failure(contentResult.ErrorMessage!);
        }

        try
        {
            var data = _serializer.Deserialize(contentResult.Value);
            _logger.LogInformation("Restored with rollback: {FilePath}", FilePath);
            return Result<T>.Success(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize rollback content for {FilePath}", FilePath);
            return Result<T>.FromException(ex, "Failed to deserialize rollback content");
        }
    }

    /// <summary>
    /// キャッシュされたデータを更新し、未保存フラグをセット
    /// </summary>
    public void UpdateCachedData(T data)
    {
        _cacheService.UpdateCache(data);

        if (_isAutoSaveEnabled)
        {
            MarkAsChanged();
        }
    }

    /// <summary>
    /// 自動保存を実行（キャッシュされたデータを使用）
    /// </summary>
    public virtual async Task<Result> AutoSaveAsync(CancellationToken cancellationToken = default)
    {
        if (!_isAutoSaveEnabled)
        {
            return Result.Failure("Auto-save is not enabled for this file manager");
        }

        var data = _cacheService.GetCachedData();
        if (data == null)
        {
            return Result.Failure("No cached data to save");
        }

        return await SaveAsync(data, cancellationToken);
    }

    public async Task<Result<T>> DownloadFromGitHubAsync(CancellationToken cancellationToken = default)
    {
        return await _gitHubSyncService.DownloadFromGitHubAsync(
            onSuccess: data =>
            {
                _logger.LogInformation("Downloaded from GitHub: {FilePath}", FilePath);
            },
            cancellationToken);
    }

    public async Task<Result<T>> SyncWithGitHubAsync(CancellationToken cancellationToken = default)
    {
        return await _gitHubSyncService.SyncWithGitHubAsync(
            onSuccess: data =>
            {
                _logger.LogDebug("Synced with GitHub: {FilePath}", FilePath);
            },
            cancellationToken);
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("Created directory: {Directory}", directory);
        }
    }

    public virtual void Dispose()
    {
        FileManagerRegistry.Instance.Unregister(FilePath);
        _gitHubClient?.Dispose();
        _stateLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}

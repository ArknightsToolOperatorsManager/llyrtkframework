using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Orchestration;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement;

/// <summary>
/// ファイル管理システム全体の統一インターフェース（ファサードパターン）
/// </summary>
public class FileManagementCoordinator : IDisposable
{
    private readonly FileManagerRegistry _registry;
    private readonly AutoSaveService _autoSaveService;
    private readonly BackupOrchestrator _backupOrchestrator;
    private readonly GitHubSyncOrchestrator _gitHubOrchestrator;
    private bool _disposed;

    public FileManagementCoordinator(
        ILoggerFactory loggerFactory,
        EventAggregator? eventAggregator = null)
    {
        if (loggerFactory == null)
            throw new ArgumentNullException(nameof(loggerFactory));

        _registry = FileManagerRegistry.Instance;

        _autoSaveService = new AutoSaveService(
            _registry,
            loggerFactory.CreateLogger<AutoSaveService>(),
            eventAggregator);

        _backupOrchestrator = new BackupOrchestrator(
            _registry,
            loggerFactory.CreateLogger<BackupOrchestrator>(),
            eventAggregator);

        _gitHubOrchestrator = new GitHubSyncOrchestrator(
            _registry,
            loggerFactory.CreateLogger<GitHubSyncOrchestrator>(),
            eventAggregator);
    }

    /// <summary>
    /// ファイルマネージャーレジストリへのアクセス
    /// </summary>
    public FileManagerRegistry Registry => _registry;

    #region 自動保存

    /// <summary>
    /// 自動保存を開始
    /// </summary>
    /// <param name="interval">ポーリング間隔（デフォルト: 500ms）</param>
    public void StartAutoSave(TimeSpan? interval = null)
    {
        _autoSaveService.Start(interval);
    }

    /// <summary>
    /// 自動保存を停止
    /// </summary>
    public void StopAutoSave()
    {
        _autoSaveService.Stop();
    }

    /// <summary>
    /// 自動保存が実行中かどうか
    /// </summary>
    public bool IsAutoSaveRunning => _autoSaveService.IsRunning;

    /// <summary>
    /// 即座に自動保存を実行（手動トリガー）
    /// </summary>
    public Task<Result> ExecuteAutoSaveNowAsync(CancellationToken cancellationToken = default)
    {
        return _autoSaveService.ExecuteNowAsync(cancellationToken);
    }

    #endregion

    #region バックアップ

    /// <summary>
    /// すべてのファイルをバックアップ
    /// </summary>
    public Task<Result> BackupAllAsync(CancellationToken cancellationToken = default)
    {
        return _backupOrchestrator.BackupAllAsync(cancellationToken);
    }

    /// <summary>
    /// 特定のファイルをバックアップ
    /// </summary>
    public Task<Result> BackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return _backupOrchestrator.BackupAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// 変更されたファイルのみバックアップ（差分バックアップ）
    /// </summary>
    public Task<Result> IncrementalBackupAsync(CancellationToken cancellationToken = default)
    {
        return _backupOrchestrator.IncrementalBackupAsync(cancellationToken);
    }

    /// <summary>
    /// 条件に一致するファイルをバックアップ
    /// </summary>
    public Task<Result> BackupWhereAsync(
        Predicate<IFileManager> predicate,
        CancellationToken cancellationToken = default)
    {
        return _backupOrchestrator.BackupWhereAsync(predicate, cancellationToken);
    }

    #endregion

    #region GitHub同期

    /// <summary>
    /// すべてのGitHub対応ファイルを同期
    /// </summary>
    public Task<Result> SyncAllWithGitHubAsync(CancellationToken cancellationToken = default)
    {
        return _gitHubOrchestrator.SyncAllAsync(cancellationToken);
    }

    /// <summary>
    /// 特定のファイルをGitHubと同期
    /// </summary>
    public Task<Result> SyncWithGitHubAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return _gitHubOrchestrator.SyncAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// 並列同期の最大数を指定してGitHub同期
    /// </summary>
    public Task<Result> SyncAllWithGitHubAsync(
        int maxParallelism,
        CancellationToken cancellationToken = default)
    {
        return _gitHubOrchestrator.SyncAllAsync(maxParallelism, cancellationToken);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;

        _autoSaveService.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

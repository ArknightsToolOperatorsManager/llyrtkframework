using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.GitHub;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;

namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// GitHub同期操作を提供するサービス実装
/// </summary>
public class GitHubSyncService<T> : IGitHubSyncService<T> where T : class
{
    private readonly GitHubFileSync? _gitHubFileSync;
    private readonly IFileIOService<T> _fileIOService;
    private readonly ILogger _logger;
    private readonly string _filePath;
    private readonly ReaderWriterLockSlim _lock = new();

    public GitHubSyncService(
        GitHubFileSync? gitHubFileSync,
        IFileIOService<T> fileIOService,
        ILogger logger,
        string filePath)
    {
        _gitHubFileSync = gitHubFileSync;
        _fileIOService = fileIOService;
        _logger = logger;
        _filePath = filePath;
    }

    public bool IsEnabled => _gitHubFileSync != null;

    public async Task<Result<T>> DownloadFromGitHubAsync(
        Action<T>? onSuccess = null,
        CancellationToken cancellationToken = default)
    {
        if (_gitHubFileSync == null)
        {
            return Result<T>.Failure("GitHub sync is not enabled for this file");
        }

        _lock.EnterWriteLock();
        try
        {
            // GitHubから強制ダウンロード（キャッシュ無視）
            var syncResult = await _gitHubFileSync.CheckAndSyncAsync(cancellationToken);

            if (syncResult.IsFailure)
            {
                return Result<T>.Failure(syncResult.ErrorMessage!);
            }

            // ダウンロードしたファイルをロード
            var loadResult = await _fileIOService.LoadAsync(null, cancellationToken);

            if (loadResult.IsSuccess)
            {
                _logger.LogInformation("Successfully downloaded and loaded from GitHub: {FilePath}", _filePath);
                onSuccess?.Invoke(loadResult.Value);
            }

            return loadResult;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task<Result<T>> SyncWithGitHubAsync(
        Action<T>? onSuccess = null,
        CancellationToken cancellationToken = default)
    {
        if (_gitHubFileSync == null)
        {
            return Result<T>.Failure("GitHub sync is not enabled for this file");
        }

        _lock.EnterReadLock();
        try
        {
            // GitHub同期（差分チェック付き）
            var syncResult = await _gitHubFileSync.CheckAndSyncAsync(cancellationToken);

            if (syncResult.IsFailure)
            {
                return Result<T>.Failure(syncResult.ErrorMessage!);
            }

            // 更新があった場合はファイルをロード
            if (File.Exists(_filePath))
            {
                var loadResult = await _fileIOService.LoadAsync(null, cancellationToken);

                if (loadResult.IsSuccess)
                {
                    _logger.LogDebug("Successfully synced with GitHub: {FilePath}", _filePath);
                    onSuccess?.Invoke(loadResult.Value);
                }

                return loadResult;
            }

            return Result<T>.Failure("File does not exist after sync");
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}

using llyrtkframework.FileManagement.Backup;
using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// バックアップ操作を提供するサービス実装
/// </summary>
public class BackupService : IBackupService
{
    private readonly BackupManager _backupManager;
    private readonly ILogger _logger;
    private readonly string _filePath;
    private readonly EventAggregator? _eventAggregator;
    private readonly ReaderWriterLockSlim _lock = new();

    public BackupService(
        BackupManager backupManager,
        ILogger logger,
        string filePath,
        EventAggregator? eventAggregator = null)
    {
        _backupManager = backupManager;
        _logger = logger;
        _filePath = filePath;
        _eventAggregator = eventAggregator;
    }

    public async Task<Result> CreateBackupAsync(
        Action<string>? onSuccess = null,
        CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (!File.Exists(_filePath))
            {
                return Result.Failure("Cannot backup: file does not exist");
            }

            var result = await _backupManager.CreateBackupAsync(cancellationToken);

            if (result.IsSuccess)
            {
                var latestBackupResult = _backupManager.GetLatestBackupPath();
                if (latestBackupResult.IsSuccess)
                {
                    _eventAggregator?.Publish(new BackupCreatedEvent(_filePath, latestBackupResult.Value));
                    onSuccess?.Invoke(latestBackupResult.Value);
                }
            }

            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public async Task<Result<string>> GetLatestBackupContentAsync()
    {
        _lock.EnterWriteLock();
        try
        {
            var backupPathResult = _backupManager.GetLatestBackupPath();

            if (backupPathResult.IsFailure)
            {
                return Result<string>.Failure(backupPathResult.ErrorMessage!);
            }

            var backupContent = await File.ReadAllTextAsync(backupPathResult.Value);
            _logger.LogInformation("Retrieved content from backup: {BackupPath}", backupPathResult.Value);

            return Result<string>.Success(backupContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve backup content for {FilePath}", _filePath);
            return Result<string>.FromException(ex, "Failed to retrieve backup content");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task<Result<string>> GetBackupContentWithRollbackAsync(
        RollbackOptions? options = null,
        Action<RollbackInfo>? onRollback = null,
        CancellationToken cancellationToken = default)
    {
        _lock.EnterWriteLock();
        try
        {
            var rollbackOptions = options ?? new RollbackOptions();
            var backupPathsResult = _backupManager.GetBackupPathsOrderedByDate();

            if (backupPathsResult.IsFailure)
                return Result<string>.Failure(backupPathsResult.ErrorMessage!);

            var backupPaths = backupPathsResult.Value;
            var maxRetries = rollbackOptions.MaxRetries < 0
                ? backupPaths.Count
                : Math.Min(rollbackOptions.MaxRetries, backupPaths.Count);

            if (backupPaths.Count == 0)
                return Result<string>.Failure("No backup files found for rollback");

            var triedPaths = new List<string>();
            var failureReasons = new List<string>();

            for (int i = 0; i < maxRetries; i++)
            {
                var backupPath = backupPaths[i];
                triedPaths.Add(backupPath);

                try
                {
                    var content = await File.ReadAllTextAsync(backupPath, cancellationToken);

                    if (i > 0) // 最新以外からリストア
                    {
                        _logger.LogWarning(
                            "Retrieved content from backup #{Index}: {Path} (Latest backup failed)",
                            i + 1, backupPath);

                        _eventAggregator?.Publish(new BackupRollbackEvent
                        {
                            FilePath = _filePath,
                            TriedBackupPaths = triedPaths,
                            SuccessfulBackupPath = backupPath,
                            FailureReasons = failureReasons,
                            OccurredAt = DateTime.Now
                        });
                    }

                    onRollback?.Invoke(new RollbackInfo
                    {
                        TriedBackupPaths = triedPaths,
                        SuccessfulBackupPath = backupPath,
                        FailureReasons = failureReasons
                    });

                    _logger.LogInformation("Successfully retrieved content from backup: {Path}", backupPath);
                    return Result<string>.Success(content);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read backup: {Path}", backupPath);
                    failureReasons.Add($"{Path.GetFileName(backupPath)}: {ex.Message}");

                    if (i < maxRetries - 1 && rollbackOptions.RetryDelay > TimeSpan.Zero)
                        await Task.Delay(rollbackOptions.RetryDelay, cancellationToken);
                }
            }

            // 全失敗
            _eventAggregator?.Publish(new BackupRollbackEvent
            {
                FilePath = _filePath,
                TriedBackupPaths = triedPaths,
                SuccessfulBackupPath = null,
                FailureReasons = failureReasons,
                OccurredAt = DateTime.Now
            });

            onRollback?.Invoke(new RollbackInfo
            {
                TriedBackupPaths = triedPaths,
                SuccessfulBackupPath = null,
                FailureReasons = failureReasons
            });

            var errorMsg = $"All {maxRetries} backup retrieve attempts failed";
            _logger.LogError(errorMsg);

            if (rollbackOptions.ThrowOnAllFailed)
                throw new InvalidOperationException(errorMsg);

            return Result<string>.Failure(errorMsg);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}

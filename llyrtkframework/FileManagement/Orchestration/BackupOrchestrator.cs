using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Orchestration;

/// <summary>
/// バックアップ処理のオーケストレーター
/// </summary>
public class BackupOrchestrator
{
    private readonly FileManagerRegistry _registry;
    private readonly ILogger<BackupOrchestrator> _logger;
    private readonly EventAggregator? _eventAggregator;

    public BackupOrchestrator(
        FileManagerRegistry registry,
        ILogger<BackupOrchestrator> logger,
        EventAggregator? eventAggregator = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventAggregator = eventAggregator;
    }

    /// <summary>
    /// すべてのファイルをバックアップ
    /// </summary>
    public async Task<Result> BackupAllAsync(CancellationToken cancellationToken = default)
    {
        var managers = _registry.GetAllManagers()
            .Select(reg => reg.Manager)
            .ToList();

        _logger.LogInformation("Starting backup for all files ({Count} files)", managers.Count);

        var tasks = managers
            .Select(manager => manager.CreateBackupAsync(cancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
            _logger.LogError("Backup failed for some files: {Errors}", errorMessages);
            return Result.Failure($"Backup partially failed: {errorMessages}");
        }

        _logger.LogInformation("Backup completed successfully for all files");
        _eventAggregator?.Publish(new GlobalBackupCompletedEvent(managers.Count));

        return Result.Success();
    }

    /// <summary>
    /// 特定のファイルをバックアップ
    /// </summary>
    public async Task<Result> BackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFullPath(filePath);
        var manager = _registry.GetManager(key);

        if (manager == null)
        {
            return Result.Failure($"File manager not found: {filePath}");
        }

        return await manager.CreateBackupAsync(cancellationToken);
    }

    /// <summary>
    /// 変更されたファイルのみバックアップ（差分バックアップ）
    /// </summary>
    public async Task<Result> IncrementalBackupAsync(CancellationToken cancellationToken = default)
    {
        var modifiedManagers = _registry.GetAllManagers()
            .Where(reg => reg.Manager.HasChangesSinceBackup)
            .Select(reg => reg.Manager)
            .ToList();

        if (!modifiedManagers.Any())
        {
            _logger.LogInformation("No modified files, skipping backup");
            return Result.Success();
        }

        _logger.LogInformation("Starting incremental backup ({Count} files)", modifiedManagers.Count);

        var tasks = modifiedManagers
            .Select(manager => manager.CreateBackupAsync(cancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);
        return Result.Combine(results);
    }

    /// <summary>
    /// 条件に一致するファイルをバックアップ
    /// </summary>
    public async Task<Result> BackupWhereAsync(
        Predicate<IFileManager> predicate,
        CancellationToken cancellationToken = default)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        var managers = _registry.GetAllManagers()
            .Where(reg => predicate(reg.Manager))
            .Select(reg => reg.Manager)
            .ToList();

        if (!managers.Any())
        {
            _logger.LogInformation("No files matched the condition, skipping backup");
            return Result.Success();
        }

        _logger.LogInformation("Starting conditional backup ({Count} files)", managers.Count);

        var tasks = managers
            .Select(manager => manager.CreateBackupAsync(cancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);
        return Result.Combine(results);
    }
}

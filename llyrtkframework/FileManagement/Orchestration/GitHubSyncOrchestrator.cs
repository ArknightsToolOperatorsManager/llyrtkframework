using llyrtkframework.FileManagement.Core;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Orchestration;

/// <summary>
/// GitHub同期処理のオーケストレーター
/// </summary>
public class GitHubSyncOrchestrator
{
    private readonly FileManagerRegistry _registry;
    private readonly ILogger<GitHubSyncOrchestrator> _logger;
    private readonly EventAggregator? _eventAggregator;

    public GitHubSyncOrchestrator(
        FileManagerRegistry registry,
        ILogger<GitHubSyncOrchestrator> logger,
        EventAggregator? eventAggregator = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventAggregator = eventAggregator;
    }

    /// <summary>
    /// すべてのGitHub対応ファイルを同期
    /// </summary>
    public async Task<Result> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var gitHubSyncs = _registry.GetAllManagers()
            .Where(reg => reg.GitHubSync != null)
            .Select(reg => reg.GitHubSync!)
            .ToList();

        if (!gitHubSyncs.Any())
        {
            _logger.LogInformation("No GitHub-enabled file managers registered");
            return Result.Success();
        }

        _logger.LogInformation("Starting GitHub sync for all files ({Count} files)", gitHubSyncs.Count);

        var tasks = gitHubSyncs
            .Select(sync => sync.CheckAndSyncAsync(cancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
            _logger.LogError("GitHub sync failed for some files: {Errors}", errorMessages);
            return Result.Failure($"GitHub sync partially failed: {errorMessages}");
        }

        _logger.LogInformation("GitHub sync completed successfully for all files");
        return Result.Success();
    }

    /// <summary>
    /// 特定のファイルを同期
    /// </summary>
    public async Task<Result> SyncAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFullPath(filePath);
        var registration = _registry.GetAllManagers()
            .FirstOrDefault(reg => reg.Manager.FilePath == key);

        if (registration == null)
        {
            return Result.Failure($"File manager not found: {filePath}");
        }

        if (registration.GitHubSync == null)
        {
            return Result.Failure($"GitHub sync is not enabled for: {filePath}");
        }

        _logger.LogInformation("Starting GitHub sync for: {FilePath}", filePath);

        var result = await registration.GitHubSync.CheckAndSyncAsync(cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("GitHub sync completed for: {FilePath}", filePath);
        }
        else
        {
            _logger.LogError("GitHub sync failed for {FilePath}: {Error}", filePath, result.ErrorMessage);
        }

        return result;
    }

    /// <summary>
    /// 並列同期の最大数を指定して同期
    /// </summary>
    public async Task<Result> SyncAllAsync(
        int maxParallelism,
        CancellationToken cancellationToken = default)
    {
        if (maxParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxParallelism), "Must be greater than 0");

        var gitHubSyncs = _registry.GetAllManagers()
            .Where(reg => reg.GitHubSync != null)
            .Select(reg => reg.GitHubSync!)
            .ToList();

        if (!gitHubSyncs.Any())
        {
            _logger.LogInformation("No GitHub-enabled file managers registered");
            return Result.Success();
        }

        _logger.LogInformation(
            "Starting GitHub sync for all files ({Count} files) with max parallelism: {MaxParallelism}",
            gitHubSyncs.Count,
            maxParallelism);

        var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
        var tasks = gitHubSyncs
            .Select(async sync =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await sync.CheckAndSyncAsync(cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .ToList();

        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
            _logger.LogError("GitHub sync failed for some files: {Errors}", errorMessages);
            return Result.Failure($"GitHub sync partially failed: {errorMessages}");
        }

        _logger.LogInformation("GitHub sync completed successfully for all files");
        return Result.Success();
    }
}

using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.GitHub;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Triggers;

/// <summary>
/// GitHub同期トリガー（ポーリング方式）
/// </summary>
public class GitHubBackupTrigger : BackupTrigger
{
    private readonly GitHubFileOptions _githubOptions;
    private readonly GitHubClient _gitHubClient;
    private readonly ILogger _logger;
    private readonly EventAggregator? _eventAggregator;
    private Timer? _timer;
    private GitHubFileSync? _fileSync;
    private bool _isFirstRun = true;

    public override BackupTriggerType Type => BackupTriggerType.GitHubSync;

    public GitHubBackupTrigger(
        GitHubFileOptions githubOptions,
        ILogger logger,
        EventAggregator? eventAggregator = null)
    {
        _githubOptions = githubOptions;
        _logger = logger;
        _eventAggregator = eventAggregator;
        _gitHubClient = new GitHubClient(logger);
    }

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive)
            return;

        // GitHubFileSyncインスタンス作成
        _fileSync = new GitHubFileSync(
            _githubOptions,
            _gitHubClient,
            fileManager,
            _logger,
            _eventAggregator);

        // タイマー設定（初回は即座に実行、その後はポーリング間隔で実行）
        var initialDelay = _isFirstRun ? TimeSpan.Zero : _githubOptions.PollingInterval;
        _timer = new Timer(
            async _ => await OnTimerCallbackAsync(),
            null,
            initialDelay,
            _githubOptions.PollingInterval);

        IsActive = true;
        _logger.LogInformation(
            "GitHub backup trigger registered for {FilePath} (polling every {Interval})",
            fileManager.FilePath,
            _githubOptions.PollingInterval);
    }

    public override void Unregister()
    {
        if (!IsActive)
            return;

        _timer?.Dispose();
        _timer = null;
        _fileSync = null;

        IsActive = false;
        _logger.LogInformation("GitHub backup trigger unregistered");
    }

    private async Task OnTimerCallbackAsync()
    {
        if (_fileSync == null)
            return;

        try
        {
            _isFirstRun = false;

            _logger.LogDebug("GitHub sync timer callback executing");
            var result = await _fileSync.CheckAndSyncAsync();

            if (result.IsFailure)
            {
                _logger.LogWarning("GitHub sync failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in GitHub sync timer callback");
        }
    }

    public override void Dispose()
    {
        Unregister();
        _gitHubClient?.Dispose();
        base.Dispose();
    }
}

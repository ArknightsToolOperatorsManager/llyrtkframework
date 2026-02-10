using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Diff;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.FileManagement.Utilities;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.GitHub;

/// <summary>
/// 個別ファイルのGitHub同期管理
/// </summary>
public class GitHubFileSync
{
    private readonly GitHubFileOptions _options;
    private readonly GitHubClient _gitHubClient;
    private readonly IFileManager _fileManager;
    private readonly ILogger _logger;
    private readonly EventAggregator? _eventAggregator;

    private DateTime? _lastCheckedAt;
    private DateTime? _lastRemotePushedAt;

    public string LocalFilePath => _fileManager.FilePath;

    public GitHubFileSync(
        GitHubFileOptions options,
        GitHubClient gitHubClient,
        IFileManager fileManager,
        ILogger logger,
        EventAggregator? eventAggregator = null)
    {
        _options = options;
        _gitHubClient = gitHubClient;
        _fileManager = fileManager;
        _logger = logger;
        _eventAggregator = eventAggregator;
    }

    /// <summary>
    /// リモート確認と同期実行
    /// </summary>
    public async Task<Result> CheckAndSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. CacheDuration経過チェック
            if (_lastCheckedAt.HasValue &&
                DateTime.Now - _lastCheckedAt.Value < _options.CacheDuration)
            {
                _logger.LogDebug("Skipping GitHub check (cache not expired)");
                return Result.Success();
            }

            // 2. pushed_at取得
            var pushedAtResult = await _gitHubClient.GetRepositoryLastPushedAtAsync(
                _options.Owner,
                _options.Repository,
                _options.Token,
                cancellationToken);

            if (pushedAtResult.IsFailure)
            {
                _logger.LogWarning("Failed to get repository pushed_at: {Error}", pushedAtResult.ErrorMessage);
                return pushedAtResult;
            }

            var remotePushedAt = pushedAtResult.Value;

            // 3. 前回のpushed_atと比較
            if (_lastRemotePushedAt.HasValue && remotePushedAt <= _lastRemotePushedAt.Value)
            {
                _logger.LogDebug("No changes detected (pushed_at unchanged)");
                _lastCheckedAt = DateTime.Now;

                _eventAggregator?.Publish(new GitHubFileCheckedEvent
                {
                    LocalFilePath = LocalFilePath,
                    RemoteUrl = BuildRemoteUrl(),
                    HasChanges = false,
                    CheckedAt = DateTime.Now
                });

                return Result.Success();
            }

            // 4. リモートSHA256取得
            var remoteSha256Result = await _gitHubClient.GetFileSha256Async(
                _options.Owner,
                _options.Repository,
                _options.Branch,
                _options.FilePath,
                _options.Token,
                cancellationToken);

            if (remoteSha256Result.IsFailure)
            {
                _logger.LogWarning("Failed to get remote file SHA256: {Error}", remoteSha256Result.ErrorMessage);
                return remoteSha256Result;
            }

            var remoteSha256 = remoteSha256Result.Value;

            // 5. ローカルSHA256計算
            if (!_fileManager.FileExists)
            {
                _logger.LogInformation("Local file does not exist, will download from remote");
            }
            else
            {
                var localSha256 = await HashUtility.CalculateSha256FromFileAsync(LocalFilePath, cancellationToken);

                // 6. ハッシュ比較
                if (localSha256 == remoteSha256)
                {
                    _logger.LogDebug("No changes detected (SHA256 match)");
                    _lastCheckedAt = DateTime.Now;
                    _lastRemotePushedAt = remotePushedAt;

                    _eventAggregator?.Publish(new GitHubFileCheckedEvent
                    {
                        LocalFilePath = LocalFilePath,
                        RemoteUrl = BuildRemoteUrl(),
                        HasChanges = false,
                        CheckedAt = DateTime.Now
                    });

                    return Result.Success();
                }

                _logger.LogInformation("File changed detected (SHA256 mismatch)");
            }

            // 7. ローカルバックアップ作成
            var backupResult = await _fileManager.CreateBackupAsync(cancellationToken);
            var backupPath = "";

            if (backupResult.IsSuccess)
            {
                _logger.LogInformation("Local backup created before GitHub sync");
                // バックアップパスを取得（簡易的にタイムスタンプベースで推測）
                backupPath = $"{LocalFilePath}.bak";
            }
            else
            {
                _logger.LogWarning("Failed to create backup: {Error}", backupResult.ErrorMessage);
            }

            // 8. リモート内容取得
            var remoteContentResult = await _gitHubClient.GetFileContentAsync(
                _options.Owner,
                _options.Repository,
                _options.Branch,
                _options.FilePath,
                _options.Token,
                cancellationToken);

            if (remoteContentResult.IsFailure)
            {
                _logger.LogError("Failed to get remote file content: {Error}", remoteContentResult.ErrorMessage);
                return remoteContentResult;
            }

            var remoteContent = remoteContentResult.Value;

            // 9. JSON差分検出
            JsonDiffReport? diffReport = null;
            if (LocalFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var oldContent = _fileManager.FileExists
                    ? await File.ReadAllTextAsync(LocalFilePath, cancellationToken)
                    : null;

                diffReport = JsonDiffDetector.DetectDifferences(oldContent, remoteContent);
                _logger.LogInformation("JSON diff detected: {Summary}", diffReport.GetSummary());
            }

            // 10. ローカルファイル上書き
            await File.WriteAllTextAsync(LocalFilePath, remoteContent, cancellationToken);
            _logger.LogInformation("Local file updated from GitHub");

            // 11. イベント発行
            _eventAggregator?.Publish(new GitHubFileUpdatedEvent
            {
                LocalFilePath = LocalFilePath,
                BackupFilePath = backupPath,
                RemoteUrl = BuildRemoteUrl(),
                DiffReport = diffReport,
                UpdatedAt = DateTime.Now
            });

            // 12. キャッシュ更新
            _lastCheckedAt = DateTime.Now;
            _lastRemotePushedAt = remotePushedAt;

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub sync failed for {FilePath}", LocalFilePath);
            return Result.FromException(ex, "GitHub sync failed");
        }
    }

    private string BuildRemoteUrl()
    {
        return $"https://github.com/{_options.Owner}/{_options.Repository}/blob/{_options.Branch}/{_options.FilePath}";
    }
}

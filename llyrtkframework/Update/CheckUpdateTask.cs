using llyrtkframework.Application;
using llyrtkframework.Notifications;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;

namespace llyrtkframework.Update;

/// <summary>
/// UI Setupタスク: アプリケーション更新チェック
/// </summary>
public class CheckUpdateTask : IUiSetupTask
{
    private readonly IUpdateChecker _updateChecker;
    private readonly INotificationService? _notificationService;
    private readonly Version _currentVersion;
    private readonly ILogger<CheckUpdateTask> _logger;

    public CheckUpdateTask(
        IUpdateChecker updateChecker,
        Version currentVersion,
        ILogger<CheckUpdateTask> logger,
        INotificationService? notificationService = null)
    {
        _updateChecker = updateChecker;
        _currentVersion = currentVersion;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for application updates");

            var result = await _updateChecker.CheckForUpdateAsync(_currentVersion, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogWarning("Update check failed: {Error}", result.ErrorMessage);
                // 更新チェック失敗でもアプリ起動は続行
                return Result.Success();
            }

            var updateInfo = result.Value!;

            if (updateInfo.IsUpdateAvailable)
            {
                _logger.LogInformation(
                    "Update available: {Current} -> {Latest}",
                    updateInfo.CurrentVersion,
                    updateInfo.LatestVersion
                );

                // 通知サービスがあれば通知
                if (_notificationService != null)
                {
                    await _notificationService.SendAsync(
                        "更新があります",
                        $"バージョン {updateInfo.LatestVersion} が利用可能です",
                        NotificationType.Information
                    );
                }
            }
            else
            {
                _logger.LogInformation("Application is up to date");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check threw exception");
            // 例外が発生してもアプリ起動は続行
            return Result.Success();
        }
    }
}

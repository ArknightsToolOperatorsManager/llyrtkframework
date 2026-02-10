using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace llyrtkframework.Application;

/// <summary>
/// アプリケーションバージョン管理
/// </summary>
public class ApplicationVersionManager
{
    private readonly ILogger<ApplicationVersionManager> _logger;
    private readonly string _versionFilePath;

    public ApplicationVersionManager(
        ILogger<ApplicationVersionManager> logger,
        string applicationDataPath)
    {
        _logger = logger;
        _versionFilePath = Path.Combine(applicationDataPath, "version.json");
    }

    /// <summary>
    /// 現在のバージョンを取得
    /// </summary>
    public Version GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        return assembly?.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    /// <summary>
    /// 保存されている前回のバージョンを取得
    /// </summary>
    public Result<Version?> GetPreviousVersion()
    {
        try
        {
            if (!File.Exists(_versionFilePath))
            {
                _logger.LogInformation("No previous version file found");
                return Result<Version?>.Success(null);
            }

            var json = File.ReadAllText(_versionFilePath);
            var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);

            if (versionInfo?.Version != null && Version.TryParse(versionInfo.Version, out var version))
            {
                _logger.LogInformation("Previous version: {Version}", version);
                return Result<Version?>.Success(version);
            }

            return Result<Version?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get previous version");
            return Result<Version?>.FromException(ex, "Failed to get previous version");
        }
    }

    /// <summary>
    /// 現在のバージョンを保存
    /// </summary>
    public Result SaveCurrentVersion()
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            var versionInfo = new VersionInfo
            {
                Version = currentVersion.ToString(),
                Timestamp = DateTime.UtcNow
            };

            var directory = Path.GetDirectoryName(_versionFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_versionFilePath, json);

            _logger.LogInformation("Current version saved: {Version}", currentVersion);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save current version");
            return Result.FromException(ex, "Failed to save current version");
        }
    }

    /// <summary>
    /// バージョンが変更されたかチェック
    /// </summary>
    public Result<bool> IsVersionChanged()
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            var previousVersionResult = GetPreviousVersion();

            if (previousVersionResult.IsFailure)
            {
                return Result<bool>.Failure(previousVersionResult.ErrorMessage ?? "Unknown error");
            }

            var previousVersion = previousVersionResult.Value;
            if (previousVersion == null)
            {
                _logger.LogInformation("No previous version found (first run)");
                return Result<bool>.Success(false);
            }

            var isChanged = currentVersion != previousVersion;
            if (isChanged)
            {
                _logger.LogInformation(
                    "Version changed: {PreviousVersion} -> {CurrentVersion}",
                    previousVersion,
                    currentVersion
                );
            }

            return Result<bool>.Success(isChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check version change");
            return Result<bool>.FromException(ex, "Failed to check version change");
        }
    }

    /// <summary>
    /// 初回起動かどうかをチェック
    /// </summary>
    public Result<bool> IsFirstRun()
    {
        try
        {
            var previousVersionResult = GetPreviousVersion();
            if (previousVersionResult.IsFailure)
            {
                return Result<bool>.Failure(previousVersionResult.ErrorMessage ?? "Unknown error");
            }

            var isFirstRun = previousVersionResult.Value == null;
            _logger.LogInformation("Is first run: {IsFirstRun}", isFirstRun);
            return Result<bool>.Success(isFirstRun);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check first run");
            return Result<bool>.FromException(ex, "Failed to check first run");
        }
    }
}

/// <summary>
/// バージョン情報
/// </summary>
internal class VersionInfo
{
    public string Version { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Pre-bootタスク: バージョンチェック
/// </summary>
public class VersionCheckTask : IPreBootTask
{
    private readonly ApplicationVersionManager _versionManager;
    private readonly ILogger<VersionCheckTask> _logger;

    public VersionCheckTask(
        ApplicationVersionManager versionManager,
        ILogger<VersionCheckTask> logger)
    {
        _versionManager = versionManager;
        _logger = logger;
    }

    public Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking application version");

        var currentVersion = _versionManager.GetCurrentVersion();
        _logger.LogInformation("Current version: {Version}", currentVersion);

        var isFirstRunResult = _versionManager.IsFirstRun();
        if (isFirstRunResult.IsSuccess && isFirstRunResult.Value)
        {
            _logger.LogInformation("First run detected");
        }

        var isVersionChangedResult = _versionManager.IsVersionChanged();
        if (isVersionChangedResult.IsSuccess && isVersionChangedResult.Value)
        {
            _logger.LogInformation("Version upgrade detected");
        }

        // バージョンを保存
        var saveResult = _versionManager.SaveCurrentVersion();
        if (saveResult.IsFailure)
        {
            _logger.LogWarning("Failed to save version: {Error}", saveResult.ErrorMessage);
            // バージョン保存失敗はアプリ起動をブロックしない
        }

        return Task.FromResult(Result.Success());
    }
}

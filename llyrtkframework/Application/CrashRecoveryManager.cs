using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace llyrtkframework.Application;

/// <summary>
/// クラッシュ検出とバックアップ復元を管理
/// </summary>
public class CrashRecoveryManager
{
    private readonly ILogger<CrashRecoveryManager> _logger;
    private readonly string _crashFlagPath;
    private readonly string _backupDirectory;

    public CrashRecoveryManager(
        ILogger<CrashRecoveryManager> logger,
        string applicationDataPath)
    {
        _logger = logger;
        _crashFlagPath = Path.Combine(applicationDataPath, ".crash_flag");
        _backupDirectory = Path.Combine(applicationDataPath, "backups");
    }

    /// <summary>
    /// クラッシュフラグを設定（起動時）
    /// </summary>
    public Result SetCrashFlag()
    {
        try
        {
            var directory = Path.GetDirectoryName(_crashFlagPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var crashInfo = new CrashFlagInfo
            {
                Timestamp = DateTime.UtcNow,
                ProcessId = Environment.ProcessId,
                Version = GetApplicationVersion()
            };

            var json = JsonSerializer.Serialize(crashInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_crashFlagPath, json);

            _logger.LogDebug("Crash flag set: {Path}", _crashFlagPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set crash flag");
            return Result.FromException(ex, "Failed to set crash flag");
        }
    }

    /// <summary>
    /// クラッシュフラグをクリア（正常終了時）
    /// </summary>
    public Result ClearCrashFlag()
    {
        try
        {
            if (File.Exists(_crashFlagPath))
            {
                File.Delete(_crashFlagPath);
                _logger.LogDebug("Crash flag cleared");
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear crash flag");
            return Result.FromException(ex, "Failed to clear crash flag");
        }
    }

    /// <summary>
    /// 前回クラッシュしたかどうかをチェック
    /// </summary>
    public Result<bool> CheckPreviousCrash()
    {
        try
        {
            if (!File.Exists(_crashFlagPath))
            {
                _logger.LogInformation("No previous crash detected");
                return Result<bool>.Success(false);
            }

            // フラグファイルを読み込む
            var json = File.ReadAllText(_crashFlagPath);
            var crashInfo = JsonSerializer.Deserialize<CrashFlagInfo>(json);

            if (crashInfo != null)
            {
                _logger.LogWarning(
                    "Previous crash detected at {Timestamp} (PID: {ProcessId}, Version: {Version})",
                    crashInfo.Timestamp,
                    crashInfo.ProcessId,
                    crashInfo.Version
                );
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check previous crash");
            return Result<bool>.FromException(ex, "Failed to check previous crash");
        }
    }

    /// <summary>
    /// バックアップディレクトリを取得
    /// </summary>
    public string GetBackupDirectory() => _backupDirectory;

    /// <summary>
    /// 最新のバックアップファイルを取得
    /// </summary>
    public Result<string?> GetLatestBackup(string filePattern = "*")
    {
        try
        {
            if (!Directory.Exists(_backupDirectory))
            {
                _logger.LogInformation("Backup directory does not exist: {Directory}", _backupDirectory);
                return Result<string?>.Success(null);
            }

            var backupFiles = Directory.GetFiles(_backupDirectory, filePattern)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            if (backupFiles.Count == 0)
            {
                _logger.LogInformation("No backup files found");
                return Result<string?>.Success(null);
            }

            var latestBackup = backupFiles.First();
            _logger.LogInformation("Latest backup found: {Path}", latestBackup);
            return Result<string?>.Success(latestBackup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest backup");
            return Result<string?>.FromException(ex, "Failed to get latest backup");
        }
    }

    /// <summary>
    /// バックアップからファイルを復元
    /// </summary>
    public async Task<Result> RestoreFromBackupAsync(
        string backupFilePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(backupFilePath))
            {
                return Result.Failure($"Backup file not found: {backupFilePath}");
            }

            _logger.LogInformation("Restoring from backup: {BackupPath} -> {DestinationPath}", backupFilePath, destinationPath);

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 既存ファイルがあればバックアップ
            if (File.Exists(destinationPath))
            {
                var corruptedPath = $"{destinationPath}.corrupted";
                File.Move(destinationPath, corruptedPath, overwrite: true);
                _logger.LogInformation("Existing file backed up as: {CorruptedPath}", corruptedPath);
            }

            // バックアップからコピー
            await using var sourceStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destStream, cancellationToken);

            _logger.LogInformation("File restored successfully from backup");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore from backup");
            return Result.FromException(ex, "Failed to restore from backup");
        }
    }

    private static string GetApplicationVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }
}

/// <summary>
/// クラッシュフラグ情報
/// </summary>
internal class CrashFlagInfo
{
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Pre-bootタスク: クラッシュチェックと復元
/// </summary>
public class CrashRecoveryTask : IPreBootTask
{
    private readonly CrashRecoveryManager _recoveryManager;
    private readonly ILogger<CrashRecoveryTask> _logger;
    private readonly Func<Task<Result>>? _restoreCallback;

    public CrashRecoveryTask(
        CrashRecoveryManager recoveryManager,
        ILogger<CrashRecoveryTask> logger,
        Func<Task<Result>>? restoreCallback = null)
    {
        _recoveryManager = recoveryManager;
        _logger = logger;
        _restoreCallback = restoreCallback;
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking for previous crash");

        // クラッシュチェック
        var crashCheckResult = _recoveryManager.CheckPreviousCrash();
        if (crashCheckResult.IsFailure)
        {
            return Result.Failure(crashCheckResult.ErrorMessage ?? "Unknown error");
        }

        // クラッシュが検出された場合
        if (crashCheckResult.Value)
        {
            _logger.LogWarning("Previous crash detected, attempting recovery");

            // カスタム復元処理があれば実行
            if (_restoreCallback != null)
            {
                var restoreResult = await _restoreCallback();
                if (restoreResult.IsFailure)
                {
                    _logger.LogError("Failed to restore from backup: {Error}", restoreResult.ErrorMessage);
                    // 復元失敗でもアプリは起動を続ける
                }
                else
                {
                    _logger.LogInformation("Successfully restored from backup");
                }
            }
        }

        // 新しいクラッシュフラグを設定
        var setFlagResult = _recoveryManager.SetCrashFlag();
        if (setFlagResult.IsFailure)
        {
            return setFlagResult;
        }

        return Result.Success();
    }
}

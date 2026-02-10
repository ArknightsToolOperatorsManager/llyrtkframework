using llyrtkframework.FileManagement.Events;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;

namespace llyrtkframework.FileManagement.Backup;

/// <summary>
/// ファイルのバックアップを管理するクラス
/// </summary>
public class BackupManager
{
    private readonly string _originalFilePath;
    private readonly BackupOptions _options;
    private readonly ILogger _logger;
    private readonly string _backupDirectory;

    public BackupManager(string originalFilePath, BackupOptions options, ILogger logger)
    {
        _originalFilePath = Path.GetFullPath(originalFilePath);
        _options = options;
        _logger = logger;

        // バックアップディレクトリを決定
        if (!string.IsNullOrEmpty(_options.BackupDirectory))
        {
            _backupDirectory = Path.GetFullPath(_options.BackupDirectory);
        }
        else
        {
            var dir = Path.GetDirectoryName(_originalFilePath);
            _backupDirectory = Path.Combine(dir ?? ".", ".backup");
        }

        EnsureBackupDirectoryExists();
    }

    /// <summary>
    /// バックアップを作成します
    /// </summary>
    public async Task<Result> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_originalFilePath))
            {
                return Result.Failure($"Original file not found: {_originalFilePath}");
            }

            var backupFileName = GenerateBackupFileName();
            var backupFilePath = Path.Combine(_backupDirectory, backupFileName);

            // ファイルをコピー
            await Task.Run(() => File.Copy(_originalFilePath, backupFilePath, overwrite: false), cancellationToken);

            _logger.LogInformation("Backup created: {BackupFilePath}", backupFilePath);

            // 古いバックアップを削除
            await CleanupOldBackupsAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup for {FilePath}", _originalFilePath);
            return Result.FromException(ex, "Failed to create backup");
        }
    }

    /// <summary>
    /// 最新のバックアップファイルパスを取得します
    /// </summary>
    public Result<string> GetLatestBackupPath()
    {
        try
        {
            var backups = GetBackupFiles();

            if (!backups.Any())
            {
                return Result<string>.Failure("No backup files found");
            }

            var latest = backups.OrderByDescending(f => f.LastWriteTime).First();
            return Result<string>.Success(latest.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest backup for {FilePath}", _originalFilePath);
            return Result<string>.FromException(ex, "Failed to get latest backup");
        }
    }

    /// <summary>
    /// すべてのバックアップファイルパスを取得します
    /// </summary>
    public Result<List<string>> GetAllBackupPaths()
    {
        try
        {
            var backups = GetBackupFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => f.FullName)
                .ToList();

            return Result<List<string>>.Success(backups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup list for {FilePath}", _originalFilePath);
            return Result<List<string>>.FromException(ex, "Failed to get backup list");
        }
    }

    /// <summary>
    /// 日付降順でソートされたバックアップファイルパスを取得します
    /// </summary>
    public Result<List<string>> GetBackupPathsOrderedByDate()
    {
        return GetAllBackupPaths();
    }

    private void EnsureBackupDirectoryExists()
    {
        if (!Directory.Exists(_backupDirectory))
        {
            Directory.CreateDirectory(_backupDirectory);
            _logger.LogDebug("Created backup directory: {Directory}", _backupDirectory);
        }
    }

    private string GenerateBackupFileName()
    {
        var fileName = Path.GetFileNameWithoutExtension(_originalFilePath);
        var extension = Path.GetExtension(_originalFilePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        return _options.BackupFilePattern
            .Replace("{filename}", fileName)
            .Replace("{timestamp}", timestamp)
            .Replace("{extension}", extension);
    }

    private FileInfo[] GetBackupFiles()
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return Array.Empty<FileInfo>();
        }

        var fileName = Path.GetFileNameWithoutExtension(_originalFilePath);
        var searchPattern = $"{fileName}_*.bak";

        var directory = new DirectoryInfo(_backupDirectory);
        return directory.GetFiles(searchPattern);
    }

    private async Task CleanupOldBackupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var backups = GetBackupFiles().OrderByDescending(f => f.LastWriteTime).ToList();

            // 最大数を超えるバックアップを削除
            if (backups.Count > _options.MaxBackupCount)
            {
                var toDelete = backups.Skip(_options.MaxBackupCount);
                foreach (var file in toDelete)
                {
                    await Task.Run(() => file.Delete(), cancellationToken);
                    _logger.LogDebug("Deleted old backup: {FilePath}", file.FullName);
                }
            }

            // 保存期間を超えるバックアップを削除
            var cutoffDate = DateTime.Now - _options.RetentionPeriod;
            var expiredBackups = backups.Where(f => f.LastWriteTime < cutoffDate);

            foreach (var file in expiredBackups)
            {
                await Task.Run(() => file.Delete(), cancellationToken);
                _logger.LogDebug("Deleted expired backup: {FilePath}", file.FullName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old backups for {FilePath}", _originalFilePath);
            // クリーンアップ失敗は致命的ではないのでログのみ
        }
    }
}

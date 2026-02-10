using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace llyrtkframework.Diagnostics;

/// <summary>
/// 診断情報エクスポート
/// ログ、設定、クラッシュレポートなどをZIPアーカイブにまとめる
/// </summary>
public class DiagnosticExporter
{
    private readonly ILogger<DiagnosticExporter> _logger;
    private readonly string _applicationDataPath;

    public DiagnosticExporter(
        ILogger<DiagnosticExporter> logger,
        string applicationDataPath)
    {
        _logger = logger;
        _applicationDataPath = applicationDataPath;
    }

    /// <summary>
    /// 診断情報をZIPファイルにエクスポート
    /// </summary>
    public async Task<Result<string>> ExportDiagnosticsAsync(
        string outputPath,
        DiagnosticExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DiagnosticExportOptions();

        try
        {
            _logger.LogInformation("Exporting diagnostics to: {OutputPath}", outputPath);

            // 一時ディレクトリを作成
            var tempDir = Path.Combine(Path.GetTempPath(), $"diagnostics_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // システム情報
                if (options.IncludeSystemInfo)
                {
                    await ExportSystemInfoAsync(tempDir, cancellationToken);
                }

                // アプリケーション情報
                if (options.IncludeApplicationInfo)
                {
                    await ExportApplicationInfoAsync(tempDir, cancellationToken);
                }

                // ログファイル
                if (options.IncludeLogs)
                {
                    await ExportLogsAsync(tempDir, options.LogFilesPattern, cancellationToken);
                }

                // 設定ファイル
                if (options.IncludeConfiguration)
                {
                    await ExportConfigurationAsync(tempDir, cancellationToken);
                }

                // クラッシュレポート
                if (options.IncludeCrashReports)
                {
                    await ExportCrashReportsAsync(tempDir, cancellationToken);
                }

                // 状態ファイル
                if (options.IncludeState)
                {
                    await ExportStateAsync(tempDir, cancellationToken);
                }

                // カスタムファイル
                if (options.CustomFilePaths != null)
                {
                    await ExportCustomFilesAsync(tempDir, options.CustomFilePaths, cancellationToken);
                }

                // ZIPアーカイブを作成
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                ZipFile.CreateFromDirectory(tempDir, outputPath);

                _logger.LogInformation("Diagnostics exported successfully: {OutputPath}", outputPath);
                return Result<string>.Success(outputPath);
            }
            finally
            {
                // 一時ディレクトリを削除
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export diagnostics");
            return Result<string>.FromException(ex, "Failed to export diagnostics");
        }
    }

    private async Task ExportSystemInfoAsync(string outputDir, CancellationToken cancellationToken)
    {
        var systemInfo = new
        {
            Timestamp = DateTime.UtcNow,
            OS = new
            {
                Version = Environment.OSVersion.ToString(),
                Platform = Environment.OSVersion.Platform.ToString(),
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                ProcessorCount = Environment.ProcessorCount
            },
            Runtime = new
            {
                CLRVersion = Environment.Version.ToString(),
                Is64BitProcess = Environment.Is64BitProcess,
                WorkingSet = Environment.WorkingSet,
                PagedMemorySize = Environment.WorkingSet
            },
            Machine = new
            {
                Name = Environment.MachineName,
                UserName = Environment.UserName,
                UserDomainName = Environment.UserDomainName,
                SystemDirectory = Environment.SystemDirectory
            },
            Paths = new
            {
                CurrentDirectory = Environment.CurrentDirectory,
                ApplicationData = _applicationDataPath,
                TempPath = Path.GetTempPath()
            }
        };

        var json = JsonSerializer.Serialize(systemInfo, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "system_info.json"),
            json,
            cancellationToken
        );
    }

    private async Task ExportApplicationInfoAsync(string outputDir, CancellationToken cancellationToken)
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;

        var appInfo = new
        {
            Timestamp = DateTime.UtcNow,
            Name = assembly?.GetName().Name ?? "Unknown",
            Version = version?.ToString() ?? "Unknown",
            Location = assembly?.Location ?? "Unknown",
            ProcessId = Environment.ProcessId,
            StartTime = DateTime.UtcNow, // 正確には Process.GetCurrentProcess().StartTime
            CommandLine = Environment.CommandLine,
            Assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => new
                {
                    Name = a.GetName().Name,
                    Version = a.GetName().Version?.ToString(),
                    Location = a.Location
                })
                .OrderBy(a => a.Name)
                .ToList()
        };

        var json = JsonSerializer.Serialize(appInfo, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "application_info.json"),
            json,
            cancellationToken
        );
    }

    private async Task ExportLogsAsync(string outputDir, string pattern, CancellationToken cancellationToken)
    {
        var logsDir = Path.Combine(_applicationDataPath, "logs");
        if (!Directory.Exists(logsDir))
        {
            _logger.LogWarning("Logs directory not found: {LogsDir}", logsDir);
            return;
        }

        var logFiles = Directory.GetFiles(logsDir, pattern);
        if (logFiles.Length == 0)
        {
            _logger.LogWarning("No log files found matching pattern: {Pattern}", pattern);
            return;
        }

        var outputLogsDir = Path.Combine(outputDir, "logs");
        Directory.CreateDirectory(outputLogsDir);

        foreach (var logFile in logFiles)
        {
            var fileName = Path.GetFileName(logFile);
            var destPath = Path.Combine(outputLogsDir, fileName);

            await using var sourceStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destStream, cancellationToken);
        }

        _logger.LogDebug("Exported {Count} log files", logFiles.Length);
    }

    private async Task ExportConfigurationAsync(string outputDir, CancellationToken cancellationToken)
    {
        var configDir = Path.Combine(_applicationDataPath, "config");
        if (!Directory.Exists(configDir))
        {
            _logger.LogWarning("Config directory not found: {ConfigDir}", configDir);
            return;
        }

        var outputConfigDir = Path.Combine(outputDir, "config");
        Directory.CreateDirectory(outputConfigDir);

        var configFiles = Directory.GetFiles(configDir, "*.json");
        foreach (var configFile in configFiles)
        {
            var fileName = Path.GetFileName(configFile);
            var destPath = Path.Combine(outputConfigDir, fileName);
            File.Copy(configFile, destPath, overwrite: true);
        }

        _logger.LogDebug("Exported {Count} config files", configFiles.Length);
        await Task.CompletedTask;
    }

    private async Task ExportCrashReportsAsync(string outputDir, CancellationToken cancellationToken)
    {
        var crashReportsDir = Path.Combine(_applicationDataPath, "crash_reports");
        if (!Directory.Exists(crashReportsDir))
        {
            _logger.LogWarning("Crash reports directory not found: {CrashReportsDir}", crashReportsDir);
            return;
        }

        var outputCrashDir = Path.Combine(outputDir, "crash_reports");
        Directory.CreateDirectory(outputCrashDir);

        var crashFiles = Directory.GetFiles(crashReportsDir, "crash_*.json")
            .OrderByDescending(File.GetCreationTimeUtc)
            .Take(10) // 最新10件のみ
            .ToList();

        foreach (var crashFile in crashFiles)
        {
            var fileName = Path.GetFileName(crashFile);
            var destPath = Path.Combine(outputCrashDir, fileName);
            File.Copy(crashFile, destPath, overwrite: true);
        }

        _logger.LogDebug("Exported {Count} crash reports", crashFiles.Count);
        await Task.CompletedTask;
    }

    private async Task ExportStateAsync(string outputDir, CancellationToken cancellationToken)
    {
        var stateFile = Path.Combine(_applicationDataPath, "state.json");
        if (!File.Exists(stateFile))
        {
            _logger.LogWarning("State file not found: {StateFile}", stateFile);
            return;
        }

        var destPath = Path.Combine(outputDir, "state.json");
        File.Copy(stateFile, destPath, overwrite: true);

        _logger.LogDebug("Exported state file");
        await Task.CompletedTask;
    }

    private async Task ExportCustomFilesAsync(
        string outputDir,
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var outputCustomDir = Path.Combine(outputDir, "custom");
        Directory.CreateDirectory(outputCustomDir);

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Custom file not found: {FilePath}", filePath);
                continue;
            }

            var fileName = Path.GetFileName(filePath);
            var destPath = Path.Combine(outputCustomDir, fileName);
            File.Copy(filePath, destPath, overwrite: true);
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// 診断情報エクスポートオプション
/// </summary>
public class DiagnosticExportOptions
{
    /// <summary>
    /// システム情報を含める
    /// </summary>
    public bool IncludeSystemInfo { get; set; } = true;

    /// <summary>
    /// アプリケーション情報を含める
    /// </summary>
    public bool IncludeApplicationInfo { get; set; } = true;

    /// <summary>
    /// ログファイルを含める
    /// </summary>
    public bool IncludeLogs { get; set; } = true;

    /// <summary>
    /// ログファイルのパターン
    /// </summary>
    public string LogFilesPattern { get; set; } = "*.log";

    /// <summary>
    /// 設定ファイルを含める
    /// </summary>
    public bool IncludeConfiguration { get; set; } = true;

    /// <summary>
    /// クラッシュレポートを含める
    /// </summary>
    public bool IncludeCrashReports { get; set; } = true;

    /// <summary>
    /// 状態ファイルを含める
    /// </summary>
    public bool IncludeState { get; set; } = true;

    /// <summary>
    /// カスタムファイルパス
    /// </summary>
    public IEnumerable<string>? CustomFilePaths { get; set; }
}

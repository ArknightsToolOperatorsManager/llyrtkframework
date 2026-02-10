using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace llyrtkframework.Diagnostics;

/// <summary>
/// グローバル例外ハンドラー
/// キャッチされない例外を処理し、診断情報を記録
/// </summary>
public class GlobalExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly string _crashReportPath;
    private readonly List<IExceptionHandler> _handlers = new();

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        string applicationDataPath)
    {
        _logger = logger;
        _crashReportPath = Path.Combine(applicationDataPath, "crash_reports");

        // クラッシュレポートディレクトリを作成
        if (!Directory.Exists(_crashReportPath))
        {
            Directory.CreateDirectory(_crashReportPath);
        }
    }

    /// <summary>
    /// 例外ハンドラーを追加
    /// </summary>
    public void RegisterHandler(IExceptionHandler handler)
    {
        _handlers.Add(handler);
        _logger.LogDebug("Exception handler registered: {HandlerType}", handler.GetType().Name);
    }

    /// <summary>
    /// グローバル例外を処理
    /// </summary>
    public async Task<Result> HandleExceptionAsync(
        Exception exception,
        string context = "Unknown",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogCritical(exception, "Unhandled exception in context: {Context}", context);

            // クラッシュレポートを生成
            var report = CreateCrashReport(exception, context);
            await SaveCrashReportAsync(report, cancellationToken);

            // 登録されたハンドラーを実行
            foreach (var handler in _handlers)
            {
                try
                {
                    var result = await handler.HandleAsync(exception, context, cancellationToken);
                    if (result.IsFailure)
                    {
                        _logger.LogWarning("Exception handler failed: {Error}", result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception handler threw exception");
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle exception");
            return Result.FromException(ex, "Failed to handle exception");
        }
    }

    /// <summary>
    /// クラッシュレポートを作成
    /// </summary>
    private CrashReport CreateCrashReport(Exception exception, string context)
    {
        return new CrashReport
        {
            Timestamp = DateTime.UtcNow,
            Context = context,
            ExceptionType = exception.GetType().FullName ?? "Unknown",
            Message = exception.Message,
            StackTrace = exception.StackTrace ?? string.Empty,
            InnerException = exception.InnerException != null
                ? new ExceptionInfo
                {
                    Type = exception.InnerException.GetType().FullName ?? "Unknown",
                    Message = exception.InnerException.Message,
                    StackTrace = exception.InnerException.StackTrace ?? string.Empty
                }
                : null,
            SystemInfo = GetSystemInfo(),
            ApplicationInfo = GetApplicationInfo()
        };
    }

    /// <summary>
    /// クラッシュレポートを保存
    /// </summary>
    private async Task SaveCrashReportAsync(CrashReport report, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = $"crash_{report.Timestamp:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(_crashReportPath, fileName);

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Crash report saved: {FilePath}", filePath);

            // 古いクラッシュレポートを削除（30日以上前）
            CleanupOldCrashReports();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save crash report");
        }
    }

    /// <summary>
    /// 古いクラッシュレポートを削除
    /// </summary>
    private void CleanupOldCrashReports()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var files = Directory.GetFiles(_crashReportPath, "crash_*.json");

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTimeUtc < cutoffDate)
                {
                    File.Delete(file);
                    _logger.LogDebug("Deleted old crash report: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old crash reports");
        }
    }

    /// <summary>
    /// システム情報を取得
    /// </summary>
    private SystemInfo GetSystemInfo()
    {
        return new SystemInfo
        {
            OSVersion = Environment.OSVersion.ToString(),
            OSArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
            ProcessorCount = Environment.ProcessorCount,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            CLRVersion = Environment.Version.ToString(),
            WorkingSet = Environment.WorkingSet,
            SystemDirectory = Environment.SystemDirectory
        };
    }

    /// <summary>
    /// アプリケーション情報を取得
    /// </summary>
    private ApplicationInfoData GetApplicationInfo()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;

        return new ApplicationInfoData
        {
            Name = assembly?.GetName().Name ?? "Unknown",
            Version = version?.ToString() ?? "Unknown",
            Location = assembly?.Location ?? "Unknown",
            ProcessId = Environment.ProcessId
        };
    }

    /// <summary>
    /// クラッシュレポート一覧を取得
    /// </summary>
    public Result<IEnumerable<string>> GetCrashReports()
    {
        try
        {
            if (!Directory.Exists(_crashReportPath))
            {
                return Result<IEnumerable<string>>.Success(Enumerable.Empty<string>());
            }

            var files = Directory.GetFiles(_crashReportPath, "crash_*.json")
                .OrderByDescending(File.GetCreationTimeUtc)
                .ToList();

            return Result<IEnumerable<string>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get crash reports");
            return Result<IEnumerable<string>>.FromException(ex, "Failed to get crash reports");
        }
    }

    /// <summary>
    /// クラッシュレポートを読み込み
    /// </summary>
    public async Task<Result<CrashReport>> LoadCrashReportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Result<CrashReport>.Failure("Crash report not found");
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var report = JsonSerializer.Deserialize<CrashReport>(json);

            if (report == null)
            {
                return Result<CrashReport>.Failure("Failed to deserialize crash report");
            }

            return Result<CrashReport>.Success(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crash report");
            return Result<CrashReport>.FromException(ex, "Failed to load crash report");
        }
    }
}

/// <summary>
/// 例外ハンドラーインターフェース
/// </summary>
public interface IExceptionHandler
{
    Task<Result> HandleAsync(Exception exception, string context, CancellationToken cancellationToken = default);
}

/// <summary>
/// クラッシュレポート
/// </summary>
public class CrashReport
{
    public DateTime Timestamp { get; set; }
    public string Context { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public ExceptionInfo? InnerException { get; set; }
    public SystemInfo SystemInfo { get; set; } = new();
    public ApplicationInfoData ApplicationInfo { get; set; } = new();
}

/// <summary>
/// 例外情報
/// </summary>
public class ExceptionInfo
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
}

/// <summary>
/// システム情報
/// </summary>
public class SystemInfo
{
    public string OSVersion { get; set; } = string.Empty;
    public string OSArchitecture { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string CLRVersion { get; set; } = string.Empty;
    public long WorkingSet { get; set; }
    public string SystemDirectory { get; set; } = string.Empty;
}

/// <summary>
/// アプリケーション情報データ
/// </summary>
public class ApplicationInfoData
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int ProcessId { get; set; }
}

/// <summary>
/// 例外ハンドラー: ファイルに記録
/// </summary>
public class FileExceptionHandler : IExceptionHandler
{
    private readonly ILogger<FileExceptionHandler> _logger;
    private readonly string _logPath;

    public FileExceptionHandler(ILogger<FileExceptionHandler> logger, string logPath)
    {
        _logger = logger;
        _logPath = logPath;
    }

    public async Task<Result> HandleAsync(
        Exception exception,
        string context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logEntry = new StringBuilder();
            logEntry.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Unhandled Exception");
            logEntry.AppendLine($"Context: {context}");
            logEntry.AppendLine($"Type: {exception.GetType().FullName}");
            logEntry.AppendLine($"Message: {exception.Message}");
            logEntry.AppendLine($"StackTrace:");
            logEntry.AppendLine(exception.StackTrace);
            logEntry.AppendLine();

            await File.AppendAllTextAsync(_logPath, logEntry.ToString(), cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log exception to file");
            return Result.FromException(ex, "Failed to log exception to file");
        }
    }
}

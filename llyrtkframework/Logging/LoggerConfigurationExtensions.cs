using Serilog;
using Serilog.Events;

namespace llyrtkframework.Logging;

/// <summary>
/// Serilog LoggerConfiguration の拡張メソッド
/// </summary>
public static class LoggerConfigurationExtensions
{
    /// <summary>
    /// デフォルトのロガー設定を作成します
    /// </summary>
    /// <param name="applicationName">アプリケーション名</param>
    /// <param name="minimumLevel">最小ログレベル（デフォルト: Debug）</param>
    /// <returns>設定済みの LoggerConfiguration</returns>
    public static LoggerConfiguration CreateDefaultConfiguration(
        string applicationName,
        LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", applicationName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: $"logs/{applicationName}-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }

    /// <summary>
    /// 本番環境向けの設定を作成します
    /// </summary>
    /// <param name="applicationName">アプリケーション名</param>
    /// <returns>設定済みの LoggerConfiguration</returns>
    public static LoggerConfiguration CreateProductionConfiguration(string applicationName)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Error)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", "Production")
            .WriteTo.File(
                path: $"logs/{applicationName}-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90,
                fileSizeLimitBytes: 100_000_000, // 100MB
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }

    /// <summary>
    /// 開発環境向けの設定を作成します（詳細ログ出力）
    /// </summary>
    /// <param name="applicationName">アプリケーション名</param>
    /// <returns>設定済みの LoggerConfiguration</returns>
    public static LoggerConfiguration CreateDevelopmentConfiguration(string applicationName)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", "Development")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: $"logs/{applicationName}-dev-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }

    /// <summary>
    /// エラーログのみを別ファイルに出力する設定を追加します
    /// </summary>
    public static LoggerConfiguration AddErrorFileLogging(
        this LoggerConfiguration loggerConfiguration,
        string applicationName)
    {
        return loggerConfiguration.WriteTo.File(
            path: $"logs/{applicationName}-errors-.log",
            rollingInterval: RollingInterval.Day,
            restrictedToMinimumLevel: LogEventLevel.Error,
            retainedFileCountLimit: 90,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }

    /// <summary>
    /// 構造化ログを JSON 形式で出力する設定を追加します
    /// </summary>
    public static LoggerConfiguration AddJsonFileLogging(
        this LoggerConfiguration loggerConfiguration,
        string applicationName)
    {
        return loggerConfiguration.WriteTo.File(
            new Serilog.Formatting.Compact.CompactJsonFormatter(),
            path: $"logs/{applicationName}-.json",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30);
    }
}

using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace llyrtkframework.Logging;

/// <summary>
/// DIコンテナへのロギング登録を行う拡張メソッド
/// </summary>
public static class LoggingServiceExtensions
{
    /// <summary>
    /// Serilog を使用したロギングを DI コンテナに登録します
    /// </summary>
    /// <param name="containerRegistry">Prism の IContainerRegistry</param>
    /// <param name="configure">Serilog の設定をカスタマイズするアクション（省略可）</param>
    /// <example>
    /// <code>
    /// // デフォルト設定で登録
    /// containerRegistry.AddLlyrtkLogging();
    ///
    /// // カスタマイズして登録
    /// containerRegistry.AddLlyrtkLogging(config =>
    /// {
    ///     config.MinimumLevel.Information()
    ///           .WriteTo.File("custom.log");
    /// });
    /// </code>
    /// </example>
    public static void AddLlyrtkLogging(
        this IContainerRegistry containerRegistry,
        Action<LoggerConfiguration>? configure = null)
    {
        var loggerConfiguration = new LoggerConfiguration();

        if (configure != null)
        {
            configure(loggerConfiguration);
        }
        else
        {
            // デフォルト設定を適用
            loggerConfiguration = LoggerConfigurationExtensions.CreateDefaultConfiguration("LlyrtkApp");
        }

        var serilogLogger = loggerConfiguration.CreateLogger();
        Log.Logger = serilogLogger;

        // Microsoft.Extensions.Logging.ILoggerFactory を登録
        var loggerFactory = new SerilogLoggerFactory(serilogLogger);
        containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);

        // ILogger<T> を解決できるように登録
        containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
    }

    /// <summary>
    /// アプリケーション名を指定してロギングを登録します
    /// </summary>
    /// <param name="containerRegistry">Prism の IContainerRegistry</param>
    /// <param name="applicationName">アプリケーション名</param>
    /// <param name="isProduction">本番環境かどうか（true: 本番設定, false: 開発設定）</param>
    public static void AddLlyrtkLogging(
        this IContainerRegistry containerRegistry,
        string applicationName,
        bool isProduction = false)
    {
        var loggerConfiguration = isProduction
            ? LoggerConfigurationExtensions.CreateProductionConfiguration(applicationName)
            : LoggerConfigurationExtensions.CreateDevelopmentConfiguration(applicationName);

        var serilogLogger = loggerConfiguration.CreateLogger();
        Log.Logger = serilogLogger;

        var loggerFactory = new SerilogLoggerFactory(serilogLogger);
        containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
        containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
    }

    /// <summary>
    /// 既に作成済みの Serilog ILogger を DI コンテナに登録します
    /// </summary>
    /// <param name="containerRegistry">Prism の IContainerRegistry</param>
    /// <param name="logger">Serilog の ILogger インスタンス</param>
    public static void AddLlyrtkLogging(
        this IContainerRegistry containerRegistry,
        Serilog.ILogger logger)
    {
        Log.Logger = logger;

        var loggerFactory = new SerilogLoggerFactory(logger);
        containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
        containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
    }
}

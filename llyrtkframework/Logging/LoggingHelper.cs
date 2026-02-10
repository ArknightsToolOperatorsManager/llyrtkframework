using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace llyrtkframework.Logging;

/// <summary>
/// ロギングに関する便利なヘルパーメソッド
/// </summary>
public static class LoggingHelper
{
    /// <summary>
    /// 処理時間を計測してログ出力するスコープを作成します
    /// </summary>
    /// <param name="logger">ILogger インスタンス</param>
    /// <param name="operationName">操作名</param>
    /// <param name="logLevel">ログレベル（デフォルト: Information）</param>
    /// <returns>IDisposable スコープ（using で使用）</returns>
    /// <example>
    /// <code>
    /// using (_logger.BeginTimedOperation("LoadData"))
    /// {
    ///     // 処理...
    /// }
    /// // 自動的に "LoadData completed in 123ms" とログ出力
    /// </code>
    /// </example>
    public static IDisposable BeginTimedOperation(
        this ILogger logger,
        string operationName,
        LogLevel logLevel = LogLevel.Information)
    {
        return new TimedOperation(logger, operationName, logLevel);
    }

    /// <summary>
    /// ログコンテキストにプロパティを追加します
    /// </summary>
    /// <param name="propertyName">プロパティ名</param>
    /// <param name="value">値</param>
    /// <returns>IDisposable スコープ（using で使用）</returns>
    /// <example>
    /// <code>
    /// using (LoggingHelper.PushProperty("UserId", userId))
    /// {
    ///     _logger.LogInformation("User action");
    ///     // ログに UserId が自動的に含まれる
    /// }
    /// </code>
    /// </example>
    public static IDisposable PushProperty(string propertyName, object value)
    {
        return LogContext.PushProperty(propertyName, value);
    }

    /// <summary>
    /// 複数のプロパティをログコンテキストに追加します
    /// </summary>
    public static IDisposable PushProperties(params (string Name, object Value)[] properties)
    {
        var disposables = properties.Select(p => LogContext.PushProperty(p.Name, p.Value)).ToList();
        return new CompositeDisposable(disposables);
    }

    /// <summary>
    /// 例外を詳細にログ出力します
    /// </summary>
    public static void LogException(
        this ILogger logger,
        Exception exception,
        string message,
        params object[] args)
    {
        logger.LogError(exception, message, args);

        // InnerException も記録
        var innerException = exception.InnerException;
        var depth = 1;
        while (innerException != null && depth <= 5)
        {
            logger.LogError("Inner Exception (Level {Depth}): {Message}",
                depth, innerException.Message);
            innerException = innerException.InnerException;
            depth++;
        }
    }

    /// <summary>
    /// 構造化ログとしてオブジェクトを記録します
    /// </summary>
    public static void LogObject<T>(
        this ILogger logger,
        string message,
        T obj,
        LogLevel logLevel = LogLevel.Information)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
                logger.LogTrace("{Message}: {@Object}", message, obj);
                break;
            case LogLevel.Debug:
                logger.LogDebug("{Message}: {@Object}", message, obj);
                break;
            case LogLevel.Information:
                logger.LogInformation("{Message}: {@Object}", message, obj);
                break;
            case LogLevel.Warning:
                logger.LogWarning("{Message}: {@Object}", message, obj);
                break;
            case LogLevel.Error:
                logger.LogError("{Message}: {@Object}", message, obj);
                break;
            case LogLevel.Critical:
                logger.LogCritical("{Message}: {@Object}", message, obj);
                break;
        }
    }

    private class TimedOperation : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly LogLevel _logLevel;
        private readonly Stopwatch _stopwatch;
        private readonly IDisposable _logContext;

        public TimedOperation(ILogger logger, string operationName, LogLevel logLevel)
        {
            _logger = logger;
            _operationName = operationName;
            _logLevel = logLevel;
            _stopwatch = Stopwatch.StartNew();
            _logContext = LogContext.PushProperty("Operation", operationName);

            LogMessage($"{operationName} started");
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            LogMessage($"{_operationName} completed in {_stopwatch.ElapsedMilliseconds}ms");
            _logContext.Dispose();
        }

        private void LogMessage(string message)
        {
            switch (_logLevel)
            {
                case LogLevel.Trace:
                    _logger.LogTrace(message);
                    break;
                case LogLevel.Debug:
                    _logger.LogDebug(message);
                    break;
                case LogLevel.Information:
                    _logger.LogInformation(message);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(message);
                    break;
                case LogLevel.Error:
                    _logger.LogError(message);
                    break;
                case LogLevel.Critical:
                    _logger.LogCritical(message);
                    break;
            }
        }
    }

    private class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables;

        public CompositeDisposable(List<IDisposable> disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}

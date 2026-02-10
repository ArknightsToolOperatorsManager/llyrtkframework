using Serilog.Core;
using Serilog.Events;

namespace llyrtkframework.Logging;

/// <summary>
/// ユーザーIDをログに追加するEnricher
/// </summary>
public class UserIdEnricher : ILogEventEnricher
{
    private readonly string _userId;

    public UserIdEnricher(string userId)
    {
        _userId = userId;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", _userId));
    }
}

/// <summary>
/// セッションIDをログに追加するEnricher
/// </summary>
public class SessionIdEnricher : ILogEventEnricher
{
    private readonly string _sessionId;

    public SessionIdEnricher(string sessionId)
    {
        _sessionId = sessionId;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SessionId", _sessionId));
    }
}

/// <summary>
/// 操作コンテキストをログに追加するEnricher
/// </summary>
public class OperationContextEnricher : ILogEventEnricher
{
    private readonly string _operationName;
    private readonly string _correlationId;

    public OperationContextEnricher(string operationName, string? correlationId = null)
    {
        _operationName = operationName;
        _correlationId = correlationId ?? Guid.NewGuid().ToString();
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Operation", _operationName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", _correlationId));
    }
}

/// <summary>
/// アプリケーションバージョンをログに追加するEnricher
/// </summary>
public class ApplicationVersionEnricher : ILogEventEnricher
{
    private readonly string _version;

    public ApplicationVersionEnricher(string version)
    {
        _version = version;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Version", _version));
    }
}

/// <summary>
/// Enricher を簡単に使用するための拡張メソッド
/// </summary>
public static class LogEnricherExtensions
{
    /// <summary>
    /// ユーザーIDをログに追加します
    /// </summary>
    public static Serilog.LoggerConfiguration WithUserId(
        this Serilog.LoggerConfiguration loggerConfiguration,
        string userId)
    {
        return loggerConfiguration.Enrich.With(new UserIdEnricher(userId));
    }

    /// <summary>
    /// セッションIDをログに追加します
    /// </summary>
    public static Serilog.LoggerConfiguration WithSessionId(
        this Serilog.LoggerConfiguration loggerConfiguration,
        string sessionId)
    {
        return loggerConfiguration.Enrich.With(new SessionIdEnricher(sessionId));
    }

    /// <summary>
    /// 操作コンテキストをログに追加します
    /// </summary>
    public static Serilog.LoggerConfiguration WithOperationContext(
        this Serilog.LoggerConfiguration loggerConfiguration,
        string operationName,
        string? correlationId = null)
    {
        return loggerConfiguration.Enrich.With(new OperationContextEnricher(operationName, correlationId));
    }

    /// <summary>
    /// アプリケーションバージョンをログに追加します
    /// </summary>
    public static Serilog.LoggerConfiguration WithApplicationVersion(
        this Serilog.LoggerConfiguration loggerConfiguration,
        string version)
    {
        return loggerConfiguration.Enrich.With(new ApplicationVersionEnricher(version));
    }
}

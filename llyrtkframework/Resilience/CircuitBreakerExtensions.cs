using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace llyrtkframework.Resilience;

/// <summary>
/// サーキットブレーカーパターンの拡張メソッド
/// </summary>
public static class CircuitBreakerExtensions
{
    /// <summary>
    /// サーキットブレーカーポリシーで非同期処理を実行し、Result を返します
    /// </summary>
    public static async Task<Result<T>> ExecuteWithCircuitBreakerAsync<T>(
        Func<Task<T>> operation,
        int exceptionsAllowedBeforeBreaking = 3,
        TimeSpan? durationOfBreak = null,
        string? errorMessage = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var breakDuration = durationOfBreak ?? TimeSpan.FromSeconds(30);

        var policy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking,
                breakDuration,
                onBreak: (exception, duration) =>
                {
                    logger?.LogWarning(exception,
                        "Circuit breaker opened for {Duration}ms. Error: {Error}",
                        duration.TotalMilliseconds, exception.Message);
                },
                onReset: () =>
                {
                    logger?.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Circuit breaker half-open, testing...");
                });

        try
        {
            var result = await policy.ExecuteAsync(async ct => await operation(), cancellationToken);
            return Result<T>.Success(result);
        }
        catch (BrokenCircuitException ex)
        {
            var message = errorMessage ?? "Circuit breaker is open";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? "Operation failed";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
    }

    /// <summary>
    /// カスタムサーキットブレーカーポリシーで非同期処理を実行し、Result を返します
    /// </summary>
    public static async Task<Result<T>> ExecuteWithPolicyAsync<T>(
        Func<Task<T>> operation,
        AsyncCircuitBreakerPolicy policy,
        string? errorMessage = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await policy.ExecuteAsync(async ct => await operation(), cancellationToken);
            return Result<T>.Success(result);
        }
        catch (BrokenCircuitException ex)
        {
            var message = errorMessage ?? "Circuit breaker is open";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? "Operation failed";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
    }
}

using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace llyrtkframework.Resilience;

/// <summary>
/// Polly と Result パターンを統合するリトライ拡張メソッド
/// </summary>
public static class RetryExtensions
{
    /// <summary>
    /// 同期処理をリトライポリシーで実行し、Result を返します
    /// </summary>
    public static Result<T> ExecuteWithRetry<T>(
        Func<T> operation,
        int maxRetryAttempts = 3,
        TimeSpan? initialDelay = null,
        string? errorMessage = null,
        ILogger? logger = null)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);

        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetry(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1) * delay.TotalSeconds),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger?.LogWarning(exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}ms. Error: {Error}",
                        retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds, exception.Message);
                });

        try
        {
            var result = policy.Execute(operation);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? $"Operation failed after {maxRetryAttempts} retries";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
    }

    /// <summary>
    /// 非同期処理をリトライポリシーで実行し、Result を返します
    /// </summary>
    public static async Task<Result<T>> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetryAttempts = 3,
        TimeSpan? initialDelay = null,
        string? errorMessage = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);

        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1) * delay.TotalSeconds),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger?.LogWarning(exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}ms. Error: {Error}",
                        retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds, exception.Message);
                });

        try
        {
            var result = await policy.ExecuteAsync(async ct => await operation(), cancellationToken);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? $"Operation failed after {maxRetryAttempts} retries";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
    }

    /// <summary>
    /// 同期処理をリトライポリシーで実行し、Result を返します（値なし）
    /// </summary>
    public static Result ExecuteWithRetry(
        Action operation,
        int maxRetryAttempts = 3,
        TimeSpan? initialDelay = null,
        string? errorMessage = null,
        ILogger? logger = null)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);

        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetry(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1) * delay.TotalSeconds),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger?.LogWarning(exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}ms. Error: {Error}",
                        retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds, exception.Message);
                });

        try
        {
            policy.Execute(operation);
            return Result.Success();
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? $"Operation failed after {maxRetryAttempts} retries";
            logger?.LogError(ex, "{Message}", message);
            return Result.Failure(message, ex);
        }
    }

    /// <summary>
    /// 非同期処理をリトライポリシーで実行し、Result を返します（値なし）
    /// </summary>
    public static async Task<Result> ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetryAttempts = 3,
        TimeSpan? initialDelay = null,
        string? errorMessage = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);

        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1) * delay.TotalSeconds),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger?.LogWarning(exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}ms. Error: {Error}",
                        retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds, exception.Message);
                });

        try
        {
            await policy.ExecuteAsync(async ct => await operation(), cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? $"Operation failed after {maxRetryAttempts} retries";
            logger?.LogError(ex, "{Message}", message);
            return Result.Failure(message, ex);
        }
    }

    /// <summary>
    /// カスタムリトライポリシーで非同期処理を実行し、Result を返します
    /// </summary>
    public static async Task<Result<T>> ExecuteWithPolicyAsync<T>(
        Func<Task<T>> operation,
        AsyncRetryPolicy policy,
        string? errorMessage = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await policy.ExecuteAsync(async ct => await operation(), cancellationToken);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? "Operation failed";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
    }

    /// <summary>
    /// 特定の例外のみリトライする非同期処理を実行し、Result を返します
    /// </summary>
    public static async Task<Result<T>> ExecuteWithRetryAsync<T, TException>(
        Func<Task<T>> operation,
        int maxRetryAttempts = 3,
        TimeSpan? initialDelay = null,
        string? errorMessage = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
        where TException : Exception
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);

        var policy = Policy
            .Handle<TException>()
            .WaitAndRetryAsync(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1) * delay.TotalSeconds),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger?.LogWarning(exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}ms. Error: {Error}",
                        retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds, exception.Message);
                });

        try
        {
            var result = await policy.ExecuteAsync(async ct => await operation(), cancellationToken);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? $"Operation failed after {maxRetryAttempts} retries";
            logger?.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
    }
}

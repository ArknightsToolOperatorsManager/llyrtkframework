# Resilience モジュール

Polly と Result パターンの統合によるレジリエンス機能

## 概要

**Resilience**モジュールは、Polly ライブラリと Result<T> パターンを統合し、リトライ、サーキットブレーカー、タイムアウトなどのレジリエンスパターンを提供します。

### 含まれるコンポーネント:
- **RetryExtensions** - リトライポリシーと Result の統合
- **CircuitBreakerExtensions** - サーキットブレーカーと Result の統合

### メリット:
- ✅ 一時的な障害からの自動回復
- ✅ エクスポネンシャルバックオフ
- ✅ サーキットブレーカーパターン
- ✅ Result パターンとのシームレスな統合
- ✅ 構造化ロギング

## 基本的な使用方法

### 1. シンプルなリトライ

```csharp
using llyrtkframework.Resilience;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;

public class UserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;

    public async Task<Result<User>> GetUserAsync(int userId)
    {
        return await RetryExtensions.ExecuteWithRetryAsync(
            operation: async () =>
            {
                var response = await _httpClient.GetAsync($"/api/users/{userId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<User>();
            },
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            errorMessage: $"Failed to get user {userId}",
            logger: _logger
        );
    }
}
```

### 2. 特定の例外のみリトライ

```csharp
public async Task<Result<Data>> FetchDataAsync()
{
    return await RetryExtensions.ExecuteWithRetryAsync<Data, HttpRequestException>(
        operation: async () =>
        {
            var response = await _httpClient.GetAsync("/api/data");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Data>();
        },
        maxRetryAttempts: 5,
        initialDelay: TimeSpan.FromMilliseconds(500),
        errorMessage: "Failed to fetch data from API",
        logger: _logger
    );
}
```

### 3. サーキットブレーカー

```csharp
public class ExternalApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalApiService> _logger;

    public async Task<Result<ApiResponse>> CallExternalApiAsync(string endpoint)
    {
        return await CircuitBreakerExtensions.ExecuteWithCircuitBreakerAsync(
            operation: async () =>
            {
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ApiResponse>();
            },
            exceptionsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromMinutes(1),
            errorMessage: $"Failed to call external API: {endpoint}",
            logger: _logger
        );
    }
}
```

## 高度な使用方法

### 1. リトライとサーキットブレーカーの組み合わせ

```csharp
using Polly;
using Polly.Wrap;

public class ResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResilientHttpClient> _logger;
    private readonly AsyncPolicyWrap _policyWrap;

    public ResilientHttpClient(HttpClient httpClient, ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // リトライポリシー
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Retry {RetryCount} after {Delay}ms",
                        retryCount, timeSpan.TotalMilliseconds);
                });

        // サーキットブレーカーポリシー
        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning(exception,
                        "Circuit broken for {Duration}ms",
                        duration.TotalMilliseconds);
                },
                onReset: () => _logger.LogInformation("Circuit reset"));

        // ポリシーをラップ（サーキットブレーカー → リトライの順）
        _policyWrap = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    public async Task<Result<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            var result = await _policyWrap.ExecuteAsync(async () =>
            {
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>();
            });

            return Result<T>.Success(result!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to GET {Endpoint}", endpoint);
            return Result<T>.Failure($"Failed to GET {endpoint}", ex);
        }
    }
}
```

### 2. タイムアウトポリシー

```csharp
using Polly.Timeout;

public class TimeoutService
{
    private readonly ILogger<TimeoutService> _logger;

    public async Task<Result<T>> ExecuteWithTimeoutAsync<T>(
        Func<Task<T>> operation,
        TimeSpan timeout,
        string? errorMessage = null)
    {
        var timeoutPolicy = Policy
            .TimeoutAsync(
                timeout,
                TimeoutStrategy.Pessimistic,
                onTimeoutAsync: (context, timespan, task) =>
                {
                    _logger.LogWarning("Operation timed out after {Timeout}ms", timespan.TotalMilliseconds);
                    return Task.CompletedTask;
                });

        try
        {
            var result = await timeoutPolicy.ExecuteAsync(operation);
            return Result<T>.Success(result);
        }
        catch (TimeoutRejectedException ex)
        {
            var message = errorMessage ?? $"Operation timed out after {timeout.TotalSeconds}s";
            _logger.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? "Operation failed";
            _logger.LogError(ex, "{Message}", message);
            return Result<T>.Failure(message, ex);
        }
    }
}
```

### 3. フォールバックポリシー

```csharp
using Polly.Fallback;

public class CacheService
{
    private readonly IApiClient _apiClient;
    private readonly ICache _cache;
    private readonly ILogger<CacheService> _logger;

    public async Task<Result<Data>> GetDataWithFallbackAsync(string key)
    {
        var fallbackPolicy = Policy<Data>
            .Handle<Exception>()
            .FallbackAsync(
                fallbackValue: await _cache.GetAsync<Data>(key) ?? new Data(),
                onFallbackAsync: async (result, context) =>
                {
                    _logger.LogWarning(result.Exception,
                        "Falling back to cached data for key {Key}", key);
                    await Task.CompletedTask;
                });

        try
        {
            var result = await fallbackPolicy.ExecuteAsync(async () =>
            {
                var data = await _apiClient.GetDataAsync(key);
                await _cache.SetAsync(key, data, TimeSpan.FromMinutes(10));
                return data;
            });

            return Result<Data>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data for key {Key}", key);
            return Result<Data>.Failure($"Failed to get data for key {key}", ex);
        }
    }
}
```

### 4. ポリシーレジストリ

```csharp
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;

public static class ResilienceServiceExtensions
{
    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services)
    {
        var registry = new PolicyRegistry
        {
            ["HttpRetry"] = Policy
                .Handle<HttpRequestException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))),

            ["HttpCircuitBreaker"] = Policy
                .Handle<HttpRequestException>()
                .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1)),

            ["DefaultTimeout"] = Policy
                .TimeoutAsync(TimeSpan.FromSeconds(30)),

            ["CombinedHttpPolicy"] = Policy.WrapAsync(
                Policy.Handle<HttpRequestException>()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))),
                Policy.Handle<HttpRequestException>()
                    .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1)),
                Policy.TimeoutAsync(TimeSpan.FromSeconds(30))
            )
        };

        services.AddSingleton<IReadOnlyPolicyRegistry<string>>(registry);
        return services;
    }
}

// 使用例
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyPolicyRegistry<string> _policyRegistry;
    private readonly ILogger<ApiService> _logger;

    public ApiService(
        HttpClient httpClient,
        IReadOnlyPolicyRegistry<string> policyRegistry,
        ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _policyRegistry = policyRegistry;
        _logger = logger;
    }

    public async Task<Result<T>> GetAsync<T>(string endpoint)
    {
        var policy = _policyRegistry.Get<IAsyncPolicy>("CombinedHttpPolicy");

        try
        {
            var result = await policy.ExecuteAsync(async () =>
            {
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>();
            });

            return Result<T>.Success(result!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to GET {Endpoint}", endpoint);
            return Result<T>.Failure($"Failed to GET {endpoint}", ex);
        }
    }
}
```

### 5. Result パターンとの深い統合

```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<OrderService> _logger;

    public async Task<Result<Order>> ProcessOrderAsync(CreateOrderRequest request)
    {
        // バリデーション → 注文作成 → 支払い処理（リトライ付き） → 保存
        return await request
            .ValidateAndReturnAsync(_validator)
            .MapAsync(validRequest => new Order
            {
                CustomerId = validRequest.CustomerId,
                Items = validRequest.Items,
                Total = validRequest.Total
            })
            .BindAsync(async order => await ProcessPaymentWithRetryAsync(order))
            .BindAsync(async order => await SaveOrderAsync(order));
    }

    private async Task<Result<Order>> ProcessPaymentWithRetryAsync(Order order)
    {
        var paymentResult = await RetryExtensions.ExecuteWithRetryAsync(
            operation: async () => await _paymentGateway.ChargeAsync(order.CustomerId, order.Total),
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(2),
            errorMessage: "Payment processing failed",
            logger: _logger
        );

        return paymentResult.IsSuccess
            ? Result<Order>.Success(order)
            : Result<Order>.Failure(paymentResult.ErrorMessage!);
    }

    private async Task<Result<Order>> SaveOrderAsync(Order order)
    {
        return await ResultExtensions.TryAsync(
            async () => await _repository.CreateAsync(order),
            "Failed to save order"
        );
    }
}
```

### 6. 条件付きリトライ

```csharp
public class SmartRetryService
{
    private readonly ILogger<SmartRetryService> _logger;

    public async Task<Result<T>> ExecuteWithConditionalRetryAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        int maxRetryAttempts = 3)
    {
        var policy = Policy
            .Handle<Exception>(shouldRetry)
            .WaitAndRetryAsync(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Conditional retry {RetryCount}/{MaxRetries} after {Delay}ms",
                        retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds);
                });

        try
        {
            var result = await policy.ExecuteAsync(operation);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed after conditional retries");
            return Result<T>.Failure("Operation failed", ex);
        }
    }
}

// 使用例
public async Task<Result<ApiResponse>> CallApiAsync(string endpoint)
{
    return await _smartRetryService.ExecuteWithConditionalRetryAsync(
        operation: async () => await _httpClient.GetFromJsonAsync<ApiResponse>(endpoint),
        shouldRetry: ex => ex is HttpRequestException httpEx &&
                          (httpEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                           httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    );
}
```

## 実践例

### HTTP クライアントでの使用

```csharp
public class ResilientApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResilientApiClient> _logger;

    public async Task<Result<T>> GetAsync<T>(string url)
    {
        return await RetryExtensions.ExecuteWithRetryAsync(
            operation: async () =>
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>();
            },
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            errorMessage: $"Failed to GET {url}",
            logger: _logger
        );
    }

    public async Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string url,
        TRequest data)
    {
        return await RetryExtensions.ExecuteWithRetryAsync(
            operation: async () =>
            {
                var response = await _httpClient.PostAsJsonAsync(url, data);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<TResponse>();
            },
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            errorMessage: $"Failed to POST to {url}",
            logger: _logger
        );
    }
}
```

### データベース操作での使用

```csharp
public class ResilientRepository<T> where T : class
{
    private readonly DbContext _context;
    private readonly ILogger<ResilientRepository<T>> _logger;

    public async Task<Result<T>> GetByIdAsync(int id)
    {
        return await RetryExtensions.ExecuteWithRetryAsync(
            operation: async () =>
            {
                var entity = await _context.Set<T>().FindAsync(id);
                if (entity == null)
                    throw new InvalidOperationException($"Entity with ID {id} not found");
                return entity;
            },
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(100),
            errorMessage: $"Failed to get entity with ID {id}",
            logger: _logger
        );
    }

    public async Task<Result<T>> CreateAsync(T entity)
    {
        return await RetryExtensions.ExecuteWithRetryAsync(
            operation: async () =>
            {
                await _context.Set<T>().AddAsync(entity);
                await _context.SaveChangesAsync();
                return entity;
            },
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(100),
            errorMessage: "Failed to create entity",
            logger: _logger
        );
    }
}
```

## ベストプラクティス

### ✅ DO

```csharp
// 適切なリトライ回数と遅延を設定
await RetryExtensions.ExecuteWithRetryAsync(
    operation,
    maxRetryAttempts: 3,  // 3回が一般的
    initialDelay: TimeSpan.FromSeconds(1)  // エクスポネンシャルバックオフ
);

// ロガーを渡してリトライをトラッキング
await RetryExtensions.ExecuteWithRetryAsync(
    operation,
    logger: _logger  // リトライをログに記録
);

// 特定の例外のみリトライ
await RetryExtensions.ExecuteWithRetryAsync<Data, HttpRequestException>(...);

// サーキットブレーカーとリトライを組み合わせる
var policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
```

### ❌ DON'T

```csharp
// 無限リトライしない
await RetryExtensions.ExecuteWithRetryAsync(
    operation,
    maxRetryAttempts: int.MaxValue  // NG: 無限ループの可能性
);

// すべての例外を無条件にリトライしない
// データ検証エラーなどはリトライしても意味がない

// 遅延なしでリトライしない
await RetryExtensions.ExecuteWithRetryAsync(
    operation,
    initialDelay: TimeSpan.Zero  // NG: サーバーに負荷をかける
);

// べき等でない操作をリトライしない
// 例: 二重課金の可能性がある支払い処理
```

## よくある質問

### Q: いつリトライを使うべき？

**A:**
- ネットワーク接続エラー
- 一時的なサービス停止
- タイムアウト
- レート制限

リトライすべきでない例:
- バリデーションエラー
- 認証エラー
- 404 Not Found

### Q: サーキットブレーカーとリトライの違いは？

**A:**
- **リトライ**: 個々のリクエストが失敗した時に再試行
- **サーキットブレーカー**: 連続して失敗した場合にサービスへのリクエストを一時停止

両方を組み合わせるのが効果的です。

### Q: パフォーマンスへの影響は？

**A:**
- リトライは失敗時のみ実行されるため、成功時は影響なし
- エクスポネンシャルバックオフで遅延が増加
- 適切な maxRetryAttempts を設定することが重要

## 関連モジュール

- [Results](../Results/README.md) - Result<T> パターンの実装
- [Logging](../Logging/README.md) - ロギング機能

## 参考リンク

- [Polly 公式ドキュメント](https://github.com/App-vNext/Polly)
- [Resilience patterns](https://docs.microsoft.com/en-us/azure/architecture/patterns/category/resiliency)

# Caching モジュール

Result パターンと統合されたキャッシング機能

## 概要

**Caching**モジュールは、Result<T> パターンと統合されたキャッシング機能を提供します。

### 含まれるコンポーネント:
- **ICache** - キャッシュのインターフェース
- **InMemoryCache** - インメモリキャッシュの実装
- **CacheExtensions** - キャッシュの拡張メソッド

### メリット:
- ✅ Result パターンとの完全な統合
- ✅ シンプルで使いやすいAPI
- ✅ 有効期限の自動管理
- ✅ スレッドセーフ
- ✅ テスタブル

## 基本的な使用方法

### 1. シンプルなキャッシュ操作

```csharp
using llyrtkframework.Caching;
using llyrtkframework.Results;
using llyrtkframework.Time;
using Microsoft.Extensions.DependencyInjection;

// DI コンテナへの登録
services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
services.AddSingleton<ICache, InMemoryCache>();

// 使用例
public class UserService
{
    private readonly ICache _cache;
    private readonly IUserRepository _repository;

    public UserService(ICache cache, IUserRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<Result<User>> GetUserAsync(int userId)
    {
        var cacheKey = $"user:{userId}";

        // キャッシュから取得を試みる
        var cachedResult = await _cache.GetAsync<User>(cacheKey);
        if (cachedResult.IsSuccess)
            return cachedResult;

        // キャッシュミスの場合、データベースから取得
        var user = await _repository.GetByIdAsync(userId);
        if (user == null)
            return Result<User>.Failure($"User {userId} not found");

        // キャッシュに保存（5分間）
        await _cache.SetAsync(cacheKey, user, TimeSpan.FromMinutes(5));

        return Result<User>.Success(user);
    }
}
```

### 2. GetOrSet パターン

```csharp
public async Task<Result<User>> GetUserAsync(int userId)
{
    var cacheKey = $"user:{userId}";

    return await _cache.GetOrSetWithResultAsync(
        key: cacheKey,
        factory: async () =>
        {
            var user = await _repository.GetByIdAsync(userId);
            return user != null
                ? Result<User>.Success(user)
                : Result<User>.Failure($"User {userId} not found");
        },
        expiration: TimeSpan.FromMinutes(5)
    );
}

// さらに簡潔に
public async Task<Result<User>> GetUserAsync(int userId)
{
    return await _cache.GetOrSetAsync(
        key: $"user:{userId}",
        factory: async () => await _repository.GetByIdAsync(userId)
                           ?? throw new InvalidOperationException("User not found"),
        expiration: TimeSpan.FromMinutes(5)
    );
}
```

### 3. キャッシュキーの生成

```csharp
public async Task<Result<List<Order>>> GetUserOrdersAsync(int userId, DateTime startDate, DateTime endDate)
{
    // ヘルパーメソッドでキーを生成
    var cacheKey = CacheExtensions.GenerateKey("user-orders", userId, startDate.ToString("yyyyMMdd"), endDate.ToString("yyyyMMdd"));
    // 結果: "user-orders:123:20240101:20240131"

    return await _cache.GetOrSetWithResultAsync(
        key: cacheKey,
        factory: async () => await _repository.GetOrdersAsync(userId, startDate, endDate),
        expiration: TimeSpan.FromMinutes(10)
    );
}
```

## 高度な使用方法

### 1. Railway Oriented Programming との統合

```csharp
public async Task<Result<UserDto>> GetUserDtoAsync(int userId)
{
    var cacheKey = $"user-dto:{userId}";

    // WithCacheAsync 拡張メソッドを使用
    return await GetUserFromDatabaseAsync(userId)
        .MapAsync(user => MapToDto(user))
        .WithCacheAsync(_cache, cacheKey, TimeSpan.FromMinutes(5));
}

private async Task<Result<User>> GetUserFromDatabaseAsync(int userId)
{
    return await ResultExtensions.TryAsync(
        async () => await _repository.GetByIdAsync(userId)
                   ?? throw new InvalidOperationException("User not found"),
        $"Failed to get user {userId}"
    );
}

private UserDto MapToDto(User user)
{
    return new UserDto
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email
    };
}
```

### 2. バッチ操作

```csharp
public async Task<Dictionary<int, Result<User>>> GetUsersAsync(List<int> userIds)
{
    var cacheKeys = userIds.Select(id => $"user:{id}").ToList();

    // バッチでキャッシュから取得
    var cachedUsers = await _cache.GetManyAsync<User>(cacheKeys);

    // キャッシュミスしたユーザーを取得
    var missedIds = userIds
        .Where((id, index) => cachedUsers[cacheKeys[index]].IsFailure)
        .ToList();

    if (missedIds.Any())
    {
        var users = await _repository.GetByIdsAsync(missedIds);

        // バッチでキャッシュに保存
        var cacheItems = users.ToDictionary(
            u => $"user:{u.Id}",
            u => u
        );

        await _cache.SetManyAsync(cacheItems, TimeSpan.FromMinutes(5));

        // 結果をマージ
        foreach (var user in users)
        {
            cachedUsers[$"user:{user.Id}"] = Result<User>.Success(user);
        }
    }

    return userIds.ToDictionary(
        id => id,
        id => cachedUsers[$"user:{id}"]
    );
}
```

### 3. キャッシュの無効化

```csharp
public class UserService
{
    private readonly ICache _cache;
    private readonly IUserRepository _repository;

    public async Task<Result<User>> UpdateUserAsync(int userId, UpdateUserRequest request)
    {
        var result = await _repository.UpdateAsync(userId, request);

        if (result.IsSuccess)
        {
            // キャッシュを無効化
            await _cache.RemoveAsync($"user:{userId}");
            await _cache.RemoveAsync($"user-dto:{userId}");
        }

        return result;
    }

    public async Task<Result> DeleteUserAsync(int userId)
    {
        var result = await _repository.DeleteAsync(userId);

        if (result.IsSuccess)
        {
            // 関連するすべてのキャッシュを削除
            await _cache.RemoveAsync($"user:{userId}");
            await _cache.RemoveAsync($"user-dto:{userId}");
            await _cache.RemoveAsync($"user-orders:{userId}");
        }

        return result;
    }
}
```

### 4. 条件付きキャッシュ

```csharp
public class ProductService
{
    private readonly ICache _cache;
    private readonly IProductRepository _repository;

    public async Task<Result<Product>> GetProductAsync(int productId, bool useCache = true)
    {
        var cacheKey = $"product:{productId}";

        if (useCache)
        {
            var cachedResult = await _cache.GetAsync<Product>(cacheKey);
            if (cachedResult.IsSuccess)
                return cachedResult;
        }

        var product = await _repository.GetByIdAsync(productId);

        if (product == null)
            return Result<Product>.Failure($"Product {productId} not found");

        if (useCache)
        {
            // 在庫がある商品のみキャッシュ
            if (product.StockQuantity > 0)
            {
                await _cache.SetAsync(cacheKey, product, TimeSpan.FromMinutes(10));
            }
        }

        return Result<Product>.Success(product);
    }
}
```

### 5. スライディング有効期限

```csharp
public class SessionService
{
    private readonly ICache _cache;
    private const int SessionTimeoutMinutes = 30;

    public async Task<Result<Session>> GetSessionAsync(string sessionId)
    {
        var cacheKey = $"session:{sessionId}";
        var result = await _cache.GetAsync<Session>(cacheKey);

        if (result.IsSuccess)
        {
            // アクセスごとに有効期限を延長（スライディング有効期限）
            await _cache.SetAsync(cacheKey, result.Value, TimeSpan.FromMinutes(SessionTimeoutMinutes));
        }

        return result;
    }

    public async Task<Result> UpdateSessionActivity(string sessionId)
    {
        var sessionResult = await GetSessionAsync(sessionId);

        if (sessionResult.IsFailure)
            return Result.Failure("Session not found");

        var session = sessionResult.Value;
        session.LastActivity = DateTime.UtcNow;

        // 有効期限を延長
        await _cache.SetAsync(
            $"session:{sessionId}",
            session,
            TimeSpan.FromMinutes(SessionTimeoutMinutes)
        );

        return Result.Success();
    }
}
```

### 6. 階層的なキャッシュキー

```csharp
public class CacheKeyBuilder
{
    public static string ForUser(int userId) => $"user:{userId}";

    public static string ForUserProfile(int userId) => $"user:{userId}:profile";

    public static string ForUserOrders(int userId) => $"user:{userId}:orders";

    public static string ForUserOrder(int userId, int orderId) => $"user:{userId}:orders:{orderId}";

    public static string ForProduct(int productId) => $"product:{productId}";

    public static string ForProductCategory(string category) => $"product:category:{category}";
}

// 使用例
public async Task<Result<UserProfile>> GetUserProfileAsync(int userId)
{
    return await _cache.GetOrSetWithResultAsync(
        key: CacheKeyBuilder.ForUserProfile(userId),
        factory: async () => await _repository.GetProfileAsync(userId),
        expiration: TimeSpan.FromMinutes(15)
    );
}
```

### 7. キャッシュのウォームアップ

```csharp
public class CacheWarmupService
{
    private readonly ICache _cache;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<CacheWarmupService> _logger;

    public async Task<Result> WarmupProductCacheAsync()
    {
        _logger.LogInformation("Starting product cache warmup");

        try
        {
            // 人気商品を取得
            var popularProducts = await _productRepository.GetPopularProductsAsync(100);

            // バッチでキャッシュに保存
            var cacheItems = popularProducts.ToDictionary(
                p => CacheKeyBuilder.ForProduct(p.Id),
                p => p
            );

            var result = await _cache.SetManyAsync(cacheItems, TimeSpan.FromHours(1));

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully warmed up cache for {Count} products", popularProducts.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warmup product cache");
            return Result.Failure("Failed to warmup cache", ex);
        }
    }
}

// アプリケーション起動時に実行
public class Startup
{
    public async Task ConfigureAsync(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var warmupService = scope.ServiceProvider.GetRequiredService<CacheWarmupService>();
        await warmupService.WarmupProductCacheAsync();
    }
}
```

## 実践例

### ECサイトでの商品キャッシュ

```csharp
public class ProductCacheService
{
    private readonly ICache _cache;
    private readonly IProductRepository _repository;
    private readonly ILogger<ProductCacheService> _logger;

    public async Task<Result<Product>> GetProductAsync(int productId)
    {
        return await _cache.GetOrSetWithResultAsync(
            key: CacheKeyBuilder.ForProduct(productId),
            factory: async () =>
            {
                _logger.LogDebug("Cache miss for product {ProductId}, fetching from database", productId);
                var product = await _repository.GetByIdAsync(productId);
                return product != null
                    ? Result<Product>.Success(product)
                    : Result<Product>.Failure($"Product {productId} not found");
            },
            expiration: TimeSpan.FromMinutes(30)
        );
    }

    public async Task<Result<List<Product>>> GetProductsByCategoryAsync(string category)
    {
        return await _cache.GetOrSetWithResultAsync(
            key: CacheKeyBuilder.ForProductCategory(category),
            factory: async () =>
            {
                _logger.LogDebug("Cache miss for category {Category}", category);
                var products = await _repository.GetByCategoryAsync(category);
                return Result<List<Product>>.Success(products);
            },
            expiration: TimeSpan.FromMinutes(10)
        );
    }

    public async Task<Result> InvalidateProductCacheAsync(int productId)
    {
        await _cache.RemoveAsync(CacheKeyBuilder.ForProduct(productId));

        // 商品が属するカテゴリのキャッシュも無効化
        var product = await _repository.GetByIdAsync(productId);
        if (product != null)
        {
            await _cache.RemoveAsync(CacheKeyBuilder.ForProductCategory(product.Category));
        }

        _logger.LogInformation("Invalidated cache for product {ProductId}", productId);
        return Result.Success();
    }
}
```

## ベストプラクティス

### ✅ DO

```csharp
// 適切な有効期限を設定
await _cache.SetAsync(key, value, TimeSpan.FromMinutes(5));

// キャッシュキーに明確な命名規則を使用
var key = $"user:{userId}:profile";

// GetOrSet パターンを使用
var result = await _cache.GetOrSetWithResultAsync(key, factory, expiration);

// 更新時にキャッシュを無効化
await _repository.UpdateAsync(user);
await _cache.RemoveAsync($"user:{user.Id}");

// 階層的なキーを使用
CacheKeyBuilder.ForUserOrder(userId, orderId);
```

### ❌ DON'T

```csharp
// 有効期限なしでキャッシュしない（メモリリーク）
await _cache.SetAsync(key, value); // NG

// 曖昧なキー名を使用しない
var key = "data"; // NG: 何のデータか不明

// キャッシュミス時の例外を無視しない
var result = await _cache.GetAsync<User>(key);
// result.IsFailure の場合の処理を忘れずに

// 大きすぎるオブジェクトをキャッシュしない
await _cache.SetAsync(key, hugeList); // NG: メモリを圧迫

// 個人情報を長期間キャッシュしない
await _cache.SetAsync(key, user, TimeSpan.FromHours(24)); // NG: セキュリティリスク
```

## よくある質問

### Q: いつキャッシュを使うべき？

**A:** 以下の場合に有効:
- 頻繁にアクセスされる読み取り専用データ
- 計算コストが高いデータ
- 外部APIからのレスポンス
- データベースクエリの結果

### Q: 適切な有効期限は？

**A:** データの性質による:
- 静的データ（設定等）: 数時間〜1日
- ユーザープロフィール: 5〜30分
- 商品情報: 10〜60分
- APIレスポンス: 1〜10分
- セッション: 20〜30分（スライディング）

### Q: 分散キャッシュが必要な場合は？

**A:** 以下の場合に検討:
- 複数のサーバーインスタンス
- スケールアウトが必要
- キャッシュの永続化が必要

Redis や Memcached などの分散キャッシュの使用を検討してください。

## パフォーマンス

- InMemoryCache は非常に高速（ナノ秒オーダー）
- ConcurrentDictionary を使用したスレッドセーフな実装
- 自動的な期限切れエントリのクリーンアップ
- メモリ効率的な実装

## 関連モジュール

- [Results](../Results/README.md) - Result<T> パターンの実装
- [Time](../Time/README.md) - 日時プロバイダー（有効期限管理に使用）

## 参考リンク

- [Caching Best Practices](https://docs.microsoft.com/en-us/azure/architecture/best-practices/caching)
- [Redis Documentation](https://redis.io/documentation)

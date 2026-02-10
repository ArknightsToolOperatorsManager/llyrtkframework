# Time モジュール

テスタブルな日時管理

## 概要

**Time**モジュールは、`DateTime.Now` や `DateTime.UtcNow` を抽象化し、テスト可能なコードを書くためのインターフェースと実装を提供します。

### 含まれるコンポーネント:
- **IDateTimeProvider** - 日時プロバイダーのインターフェース
- **SystemDateTimeProvider** - システム時刻を返す実装
- **FixedDateTimeProvider** - 固定時刻を返す実装（テスト用）
- **AdjustableDateTimeProvider** - 調整可能な時刻を返す実装（テスト用）

### メリット:
- ✅ テスタビリティの向上
- ✅ タイムゾーンの一元管理
- ✅ 時刻依存のロジックを簡単にテスト
- ✅ モックやスタブが不要

## 基本的な使用方法

### 1. 本番環境での使用

```csharp
using llyrtkframework.Time;
using Microsoft.Extensions.DependencyInjection;

// DI コンテナへの登録
services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

// サービスでの使用
public class OrderService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOrderRepository _repository;

    public OrderService(
        IDateTimeProvider dateTimeProvider,
        IOrderRepository repository)
    {
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
    }

    public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Items = request.Items,
            CreatedAt = _dateTimeProvider.UtcNow, // テスタブル！
            Status = OrderStatus.Pending
        };

        return await ResultExtensions.TryAsync(
            async () => await _repository.CreateAsync(order),
            "Failed to create order"
        );
    }

    public async Task<Result<List<Order>>> GetRecentOrdersAsync(int days)
    {
        var cutoffDate = _dateTimeProvider.UtcNow.AddDays(-days);
        return await ResultExtensions.TryAsync(
            async () => await _repository.GetOrdersSinceAsync(cutoffDate),
            "Failed to get recent orders"
        );
    }
}
```

### 2. テストでの使用（固定時刻）

```csharp
using llyrtkframework.Time;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrder_ShouldSetCorrectCreatedAt()
    {
        // Arrange
        var fixedDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var dateTimeProvider = new FixedDateTimeProvider(fixedDate);
        var repository = new InMemoryOrderRepository();
        var service = new OrderService(dateTimeProvider, repository);

        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = new List<OrderItem> { /* ... */ }
        };

        // Act
        var result = await service.CreateOrderAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(fixedDate, result.Value.CreatedAt);
    }

    [Fact]
    public async Task GetRecentOrders_ShouldReturnOrdersWithinDateRange()
    {
        // Arrange
        var now = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var dateTimeProvider = new FixedDateTimeProvider(now);
        var repository = new InMemoryOrderRepository();

        // テストデータを作成
        await repository.CreateAsync(new Order { CreatedAt = now.AddDays(-1) }); // 含まれる
        await repository.CreateAsync(new Order { CreatedAt = now.AddDays(-5) }); // 含まれる
        await repository.CreateAsync(new Order { CreatedAt = now.AddDays(-10) }); // 含まれない

        var service = new OrderService(dateTimeProvider, repository);

        // Act
        var result = await service.GetRecentOrdersAsync(7);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }
}
```

### 3. テストでの使用（調整可能な時刻）

```csharp
public class SubscriptionServiceTests
{
    [Fact]
    public async Task Subscription_ShouldExpireAfter30Days()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dateTimeProvider = new AdjustableDateTimeProvider(startDate);
        var service = new SubscriptionService(dateTimeProvider);

        var subscription = await service.CreateSubscriptionAsync(userId: 1);

        // Act - 29日後
        dateTimeProvider.Advance(TimeSpan.FromDays(29));
        var isActive29Days = await service.IsSubscriptionActiveAsync(subscription.Id);

        // Act - 30日後
        dateTimeProvider.Advance(TimeSpan.FromDays(1));
        var isActive30Days = await service.IsSubscriptionActiveAsync(subscription.Id);

        // Assert
        Assert.True(isActive29Days.Value);
        Assert.False(isActive30Days.Value);
    }

    [Fact]
    public void AdjustableProvider_CanAdvanceAndRewind()
    {
        // Arrange
        var baseDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var provider = new AdjustableDateTimeProvider(baseDate);

        // Act & Assert - 進める
        provider.Advance(TimeSpan.FromHours(2));
        Assert.Equal(new DateTime(2024, 1, 1, 14, 0, 0, DateTimeKind.Utc), provider.UtcNow);

        // Act & Assert - 戻す
        provider.Rewind(TimeSpan.FromHours(1));
        Assert.Equal(new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc), provider.UtcNow);

        // Act & Assert - リセット
        provider.ResetOffset();
        Assert.Equal(baseDate, provider.UtcNow);
    }
}
```

## 高度な使用方法

### 1. ドメインモデルでの使用

```csharp
public class Subscription : GuidEntity
{
    public Guid UserId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public SubscriptionStatus Status { get; private set; }

    private Subscription(Guid userId, DateTime startDate, DateTime endDate)
        : base()
    {
        UserId = userId;
        StartDate = startDate;
        EndDate = endDate;
        Status = SubscriptionStatus.Active;
    }

    public static Result<Subscription> Create(
        Guid userId,
        int durationInDays,
        IDateTimeProvider dateTimeProvider)
    {
        return Guard.Combine(
            Guard.AgainstEmptyGuid(userId, nameof(userId)),
            Guard.AgainstNegativeOrZero(durationInDays, nameof(durationInDays))
        ).Map(_ =>
        {
            var startDate = dateTimeProvider.UtcNow;
            var endDate = startDate.AddDays(durationInDays);
            return new Subscription(userId, startDate, endDate);
        });
    }

    public bool IsActive(IDateTimeProvider dateTimeProvider)
    {
        if (Status != SubscriptionStatus.Active)
            return false;

        var now = dateTimeProvider.UtcNow;
        return now >= StartDate && now <= EndDate;
    }

    public Result Renew(int durationInDays, IDateTimeProvider dateTimeProvider)
    {
        return Guard.AgainstNegativeOrZero(durationInDays, nameof(durationInDays))
            .OnSuccess(_ =>
            {
                var now = dateTimeProvider.UtcNow;
                StartDate = now;
                EndDate = now.AddDays(durationInDays);
                Status = SubscriptionStatus.Active;
            });
    }
}
```

### 2. スケジューリングとリマインダー

```csharp
public class ReminderService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IReminderRepository _repository;
    private readonly INotificationService _notificationService;

    public ReminderService(
        IDateTimeProvider dateTimeProvider,
        IReminderRepository repository,
        INotificationService notificationService)
    {
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
        _notificationService = notificationService;
    }

    public async Task<Result<Reminder>> ScheduleReminderAsync(
        Guid userId,
        string message,
        TimeSpan delay)
    {
        var scheduledTime = _dateTimeProvider.UtcNow.Add(delay);

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = message,
            ScheduledTime = scheduledTime,
            CreatedAt = _dateTimeProvider.UtcNow,
            Status = ReminderStatus.Pending
        };

        return await ResultExtensions.TryAsync(
            async () => await _repository.CreateAsync(reminder),
            "Failed to schedule reminder"
        );
    }

    public async Task<Result> ProcessDueRemindersAsync()
    {
        var now = _dateTimeProvider.UtcNow;
        var dueReminders = await _repository.GetDueRemindersAsync(now);

        foreach (var reminder in dueReminders)
        {
            await _notificationService.SendAsync(reminder.UserId, reminder.Message);
            reminder.Status = ReminderStatus.Sent;
            reminder.SentAt = now;
            await _repository.UpdateAsync(reminder);
        }

        return Result.Success();
    }
}

// テスト
public class ReminderServiceTests
{
    [Fact]
    public async Task ProcessDueReminders_ShouldOnlySendDueReminders()
    {
        // Arrange
        var now = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var dateTimeProvider = new AdjustableDateTimeProvider(now);
        var repository = new InMemoryReminderRepository();
        var notificationService = new FakeNotificationService();
        var service = new ReminderService(dateTimeProvider, repository, notificationService);

        // リマインダーを作成
        await service.ScheduleReminderAsync(userId, "Reminder 1", TimeSpan.FromHours(1));
        await service.ScheduleReminderAsync(userId, "Reminder 2", TimeSpan.FromHours(3));

        // Act - 2時間進める
        dateTimeProvider.Advance(TimeSpan.FromHours(2));
        await service.ProcessDueRemindersAsync();

        // Assert - 1つ目のリマインダーのみ送信される
        Assert.Equal(1, notificationService.SentCount);
    }
}
```

### 3. 有効期限の管理

```csharp
public class CacheEntry<T>
{
    public T Value { get; }
    public DateTime ExpiresAt { get; }

    public CacheEntry(T value, DateTime expiresAt)
    {
        Value = value;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(IDateTimeProvider dateTimeProvider)
    {
        return dateTimeProvider.UtcNow > ExpiresAt;
    }
}

public class SimpleCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, CacheEntry<TValue>> _cache = new();
    private readonly IDateTimeProvider _dateTimeProvider;

    public SimpleCache(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public Result<TValue> Get(TKey key)
    {
        if (!_cache.TryGetValue(key, out var entry))
            return Result<TValue>.Failure("Key not found");

        if (entry.IsExpired(_dateTimeProvider))
        {
            _cache.Remove(key);
            return Result<TValue>.Failure("Cache entry expired");
        }

        return Result<TValue>.Success(entry.Value);
    }

    public Result Set(TKey key, TValue value, TimeSpan expiration)
    {
        var expiresAt = _dateTimeProvider.UtcNow.Add(expiration);
        var entry = new CacheEntry<TValue>(value, expiresAt);
        _cache[key] = entry;
        return Result.Success();
    }

    public void RemoveExpiredEntries()
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired(_dateTimeProvider))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.Remove(key);
        }
    }
}
```

### 4. ビジネス時間の計算

```csharp
public class BusinessHoursService
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public BusinessHoursService(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public bool IsBusinessHours()
    {
        var now = _dateTimeProvider.Now; // ローカル時刻
        var hour = now.Hour;
        var dayOfWeek = now.DayOfWeek;

        // 月曜日〜金曜日の9:00〜18:00
        return dayOfWeek >= DayOfWeek.Monday
            && dayOfWeek <= DayOfWeek.Friday
            && hour >= 9
            && hour < 18;
    }

    public DateTime GetNextBusinessDay()
    {
        var date = _dateTimeProvider.Today;
        do
        {
            date = date.AddDays(1);
        }
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);

        return date;
    }

    public TimeSpan GetTimeUntilNextBusinessHours()
    {
        if (IsBusinessHours())
            return TimeSpan.Zero;

        var now = _dateTimeProvider.Now;
        var nextBusinessStart = now.Date.AddHours(9);

        // 現在時刻が18:00以降、または土日の場合
        if (now.Hour >= 18 || now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
        {
            nextBusinessStart = GetNextBusinessDay().AddHours(9);
        }

        return nextBusinessStart - now;
    }
}
```

## DI コンテナへの登録例

### ASP.NET Core / Generic Host

```csharp
public static class TimeServiceExtensions
{
    public static IServiceCollection AddTimeProvider(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        return services;
    }

    public static IServiceCollection AddFixedTimeProvider(
        this IServiceCollection services,
        DateTime fixedDateTime)
    {
        services.AddSingleton<IDateTimeProvider>(
            new FixedDateTimeProvider(fixedDateTime));
        return services;
    }

    public static IServiceCollection AddAdjustableTimeProvider(
        this IServiceCollection services,
        DateTime? baseDateTime = null)
    {
        services.AddSingleton<IDateTimeProvider>(
            baseDateTime.HasValue
                ? new AdjustableDateTimeProvider(baseDateTime.Value)
                : new AdjustableDateTimeProvider());
        return services;
    }
}

// 使用例
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // 本番環境
        services.AddTimeProvider();

        // または、テスト環境
        if (Environment.IsDevelopment())
        {
            services.AddFixedTimeProvider(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }
    }
}
```

## ベストプラクティス

### ✅ DO

```csharp
// 常に IDateTimeProvider を使用
public class UserService
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserService(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public Result<User> CreateUser(string name)
    {
        var user = new User
        {
            Name = name,
            CreatedAt = _dateTimeProvider.UtcNow // Good!
        };
        return Result<User>.Success(user);
    }
}

// UTC時刻を使用（タイムゾーンの問題を回避）
var timestamp = _dateTimeProvider.UtcNow;

// ローカル時刻が必要な場合のみ Now を使用
var displayTime = _dateTimeProvider.Now;
```

### ❌ DON'T

```csharp
// DateTime.Now を直接使用しない
public Result<User> CreateUser(string name)
{
    var user = new User
    {
        Name = name,
        CreatedAt = DateTime.UtcNow // Bad! テストできない
    };
    return Result<User>.Success(user);
}

// DateTimeKind を無視しない
var timestamp = new DateTime(2024, 1, 1); // Bad! Unspecified になる
// 代わりに
var timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); // Good!
```

## よくある質問

### Q: なぜ DateTime.Now を直接使わないの？

**A:** 以下の理由があります:
- テストで時刻を制御できない
- タイムゾーンの問題が発生しやすい
- 時刻依存のロジックをテストするのが困難

### Q: すべての DateTime.Now を置き換える必要がある？

**A:** いいえ。以下の場合に使用:
- ビジネスロジックで日時を使用
- データベースにタイムスタンプを保存
- 有効期限やスケジュールの計算

単なるログ出力などには不要です。

### Q: DateTimeOffset は？

**A:** タイムゾーン情報が重要な場合は DateTimeOffset の使用を検討してください。
IDateTimeProvider は基本的な DateTime の抽象化を提供します。

## パフォーマンス

- `IDateTimeProvider` の呼び出しは非常に軽量
- インライン化されるため、ほぼオーバーヘッドなし
- システム時刻の取得自体が高速

## 関連モジュール

- [Results](../Results/README.md) - Result<T> パターンの実装
- [Guard](../Guard/README.md) - ガード句によるバリデーション
- [Domain](../Domain/README.md) - エンティティと値オブジェクト

# Events モジュール

ReactiveUI ベースのイベント集約（Event Aggregator）

## 概要

**Events**モジュールは、疎結合なコンポーネント間通信を実現するイベント集約パターンを提供します。ReactiveUIの強力なReactive Extensions (Rx)を活用しています。

### 含まれるコンポーネント:
- **IEventAggregator** - イベント集約のインターフェース
- **EventAggregator** - ReactiveUIベースの実装

### メリット:
- ✅ コンポーネント間の疎結合
- ✅ 強力なReactive Extensions (Rx) のサポート
- ✅ 型安全なイベント
- ✅ 柔軟な購読管理
- ✅ MVVM パターンとの親和性

## 基本的な使用方法

### 1. イベントの定義

```csharp
// イベントクラスの定義
public class UserLoggedInEvent
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public DateTime LoginTime { get; set; }
}

public class OrderCreatedEvent
{
    public int OrderId { get; set; }
    public decimal Total { get; set; }
}

public class NotificationEvent
{
    public string Message { get; set; }
    public NotificationType Type { get; set; }
}

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success
}
```

### 2. イベントの発行と購読

```csharp
using llyrtkframework.Events;
using Microsoft.Extensions.DependencyInjection;

// DI コンテナへの登録
services.AddSingleton<IEventAggregator, EventAggregator>();

// イベントの発行
public class AuthenticationService
{
    private readonly IEventAggregator _eventAggregator;

    public AuthenticationService(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    public async Task<Result> LoginAsync(string username, string password)
    {
        // ログイン処理...
        var user = await _authRepository.AuthenticateAsync(username, password);

        if (user != null)
        {
            // イベントを発行
            _eventAggregator.Publish(new UserLoggedInEvent
            {
                UserId = user.Id,
                UserName = user.Name,
                LoginTime = DateTime.UtcNow
            });

            return Result.Success();
        }

        return Result.Failure("Invalid credentials");
    }
}

// イベントの購読
public class ActivityLogService
{
    private readonly IEventAggregator _eventAggregator;
    private readonly IDisposable _subscription;

    public ActivityLogService(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;

        // イベントを購読
        _subscription = _eventAggregator
            .GetEvent<UserLoggedInEvent>()
            .Subscribe(@event =>
            {
                LogActivity($"User {event.UserName} logged in at {event.LoginTime}");
            });
    }

    private void LogActivity(string message)
    {
        // ログ処理...
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
```

### 3. ViewModelでの使用

```csharp
using ReactiveUI;
using System.Reactive.Disposables;

public class MainViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly IEventAggregator _eventAggregator;
    private string _notificationMessage;

    public ViewModelActivator Activator { get; }

    public string NotificationMessage
    {
        get => _notificationMessage;
        set => this.RaiseAndSetIfChanged(ref _notificationMessage, value);
    }

    public MainViewModel(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        Activator = new ViewModelActivator();

        this.WhenActivated(disposables =>
        {
            // ViewModelがアクティブな間だけイベントを購読
            _eventAggregator
                .GetEvent<NotificationEvent>()
                .Subscribe(notification =>
                {
                    NotificationMessage = notification.Message;
                })
                .DisposeWith(disposables);

            _eventAggregator
                .GetEvent<OrderCreatedEvent>()
                .Subscribe(order =>
                {
                    NotificationMessage = $"Order #{order.OrderId} created successfully!";
                })
                .DisposeWith(disposables);
        });
    }

    public void CreateOrder()
    {
        // 注文作成処理...

        _eventAggregator.Publish(new OrderCreatedEvent
        {
            OrderId = 12345,
            Total = 99.99m
        });
    }
}
```

## 高度な使用方法

### 1. Reactive Extensions (Rx) の活用

```csharp
public class NotificationViewModel : ReactiveObject
{
    private readonly IEventAggregator _eventAggregator;

    public NotificationViewModel(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;

        // 3秒以内に同じ通知が複数来た場合は1つにまとめる
        _eventAggregator
            .GetEvent<NotificationEvent>()
            .Throttle(TimeSpan.FromSeconds(3))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(notification =>
            {
                ShowNotification(notification.Message, notification.Type);
            });

        // エラー通知のみをフィルタ
        _eventAggregator
            .GetEvent<NotificationEvent>()
            .Where(n => n.Type == NotificationType.Error)
            .Subscribe(error =>
            {
                LogError(error.Message);
            });

        // 最新の5件の通知のみを保持
        _eventAggregator
            .GetEvent<NotificationEvent>()
            .Buffer(TimeSpan.FromSeconds(1), 5)
            .Subscribe(notifications =>
            {
                UpdateNotificationList(notifications);
            });
    }
}
```

### 2. 非同期イベント処理

```csharp
public class EmailService
{
    private readonly IEventAggregator _eventAggregator;
    private readonly IEmailSender _emailSender;

    public EmailService(
        IEventAggregator eventAggregator,
        IEmailSender emailSender)
    {
        _eventAggregator = eventAggregator;
        _emailSender = emailSender;

        _eventAggregator
            .GetEvent<OrderCreatedEvent>()
            .SelectMany(async order =>
            {
                // 非同期でメール送信
                await _emailSender.SendOrderConfirmationAsync(order.OrderId);
                return order;
            })
            .Subscribe(
                order => Console.WriteLine($"Email sent for order {order.OrderId}"),
                error => Console.WriteLine($"Failed to send email: {error.Message}")
            );
    }
}
```

### 3. イベントの変換とチェーン

```csharp
public class OrderWorkflowService
{
    private readonly IEventAggregator _eventAggregator;

    public OrderWorkflowService(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;

        // OrderCreatedEvent → OrderProcessingEvent への変換
        _eventAggregator
            .GetEvent<OrderCreatedEvent>()
            .Select(created => new OrderProcessingEvent
            {
                OrderId = created.OrderId,
                Status = "Processing"
            })
            .Subscribe(processing =>
            {
                _eventAggregator.Publish(processing);
            });

        // OrderProcessingEvent → OrderShippedEvent への変換
        _eventAggregator
            .GetEvent<OrderProcessingEvent>()
            .Delay(TimeSpan.FromMinutes(5)) // 5分後に発送
            .Select(processing => new OrderShippedEvent
            {
                OrderId = processing.OrderId,
                ShippedAt = DateTime.UtcNow
            })
            .Subscribe(shipped =>
            {
                _eventAggregator.Publish(shipped);
            });
    }
}
```

### 4. エラーハンドリング

```csharp
public class ResilientEventSubscriber
{
    private readonly IEventAggregator _eventAggregator;
    private readonly ILogger<ResilientEventSubscriber> _logger;

    public ResilientEventSubscriber(
        IEventAggregator eventAggregator,
        ILogger<ResilientEventSubscriber> logger)
    {
        _eventAggregator = eventAggregator;
        _logger = logger;

        _eventAggregator
            .GetEvent<OrderCreatedEvent>()
            .Retry(3) // エラー時に3回リトライ
            .Catch<OrderCreatedEvent, Exception>(ex =>
            {
                _logger.LogError(ex, "Failed to process order event");
                return Observable.Empty<OrderCreatedEvent>();
            })
            .Subscribe(
                order => ProcessOrder(order),
                error => _logger.LogError(error, "Unhandled error in order processing")
            );
    }

    private void ProcessOrder(OrderCreatedEvent order)
    {
        // 注文処理...
    }
}
```

### 5. 複数のイベントの組み合わせ

```csharp
public class PaymentWorkflowService
{
    private readonly IEventAggregator _eventAggregator;

    public PaymentWorkflowService(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;

        // OrderCreated と PaymentAuthorized の両方を待つ
        var orderCreated = _eventAggregator.GetEvent<OrderCreatedEvent>();
        var paymentAuthorized = _eventAggregator.GetEvent<PaymentAuthorizedEvent>();

        orderCreated
            .Join(
                paymentAuthorized,
                order => orderCreated.Where(o => o.OrderId == order.OrderId),
                payment => paymentAuthorized.Where(p => p.OrderId == payment.OrderId),
                (order, payment) => new { Order = order, Payment = payment }
            )
            .Subscribe(combined =>
            {
                _eventAggregator.Publish(new OrderFulfilledEvent
                {
                    OrderId = combined.Order.OrderId,
                    Total = combined.Order.Total,
                    PaymentId = combined.Payment.PaymentId
                });
            });
    }
}
```

### 6. 条件付き購読

```csharp
public class ConditionalSubscriberService
{
    private readonly IEventAggregator _eventAggregator;
    private readonly ISettingsService _settings;

    public ConditionalSubscriberService(
        IEventAggregator eventAggregator,
        ISettingsService settings)
    {
        _eventAggregator = eventAggregator;
        _settings = settings;

        // 高額注文のみを処理
        _eventAggregator
            .GetEvent<OrderCreatedEvent>()
            .Where(order => order.Total >= 1000)
            .Subscribe(order =>
            {
                NotifyManager(order);
            });

        // 営業時間内のイベントのみ処理
        _eventAggregator
            .GetEvent<NotificationEvent>()
            .Where(_ => IsBusinessHours())
            .Subscribe(notification =>
            {
                SendPushNotification(notification);
            });

        // 有効な設定の場合のみ処理
        _eventAggregator
            .GetEvent<UserActivityEvent>()
            .Where(_ => _settings.TrackingEnabled)
            .Subscribe(activity =>
            {
                TrackActivity(activity);
            });
    }

    private bool IsBusinessHours()
    {
        var now = DateTime.Now;
        return now.Hour >= 9 && now.Hour < 18;
    }
}
```

## 実践例

### ECサイトでの注文処理

```csharp
// イベント定義
public class OrderCreatedEvent
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
}

public class PaymentProcessedEvent
{
    public int OrderId { get; set; }
    public string PaymentId { get; set; }
}

public class InventoryUpdatedEvent
{
    public int OrderId { get; set; }
    public List<int> ProductIds { get; set; }
}

// 注文サービス
public class OrderService
{
    private readonly IEventAggregator _eventAggregator;

    public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            CustomerId = request.CustomerId,
            Items = request.Items,
            Total = request.Total
        };

        await _repository.SaveAsync(order);

        // イベントを発行
        _eventAggregator.Publish(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Total = order.Total
        });

        return Result<Order>.Success(order);
    }
}

// 支払いサービス（OrderCreatedEventを購読）
public class PaymentService
{
    public PaymentService(IEventAggregator eventAggregator)
    {
        eventAggregator
            .GetEvent<OrderCreatedEvent>()
            .SelectMany(async order =>
            {
                var payment = await ProcessPaymentAsync(order);
                return new PaymentProcessedEvent
                {
                    OrderId = order.OrderId,
                    PaymentId = payment.Id
                };
            })
            .Subscribe(payment =>
            {
                eventAggregator.Publish(payment);
            });
    }
}

// 在庫サービス（PaymentProcessedEventを購読）
public class InventoryService
{
    public InventoryService(IEventAggregator eventAggregator)
    {
        eventAggregator
            .GetEvent<PaymentProcessedEvent>()
            .SelectMany(async payment =>
            {
                var productIds = await UpdateInventoryAsync(payment.OrderId);
                return new InventoryUpdatedEvent
                {
                    OrderId = payment.OrderId,
                    ProductIds = productIds
                };
            })
            .Subscribe(inventory =>
            {
                eventAggregator.Publish(inventory);
            });
    }
}

// 通知サービス（InventoryUpdatedEventを購読）
public class NotificationService
{
    public NotificationService(IEventAggregator eventAggregator)
    {
        eventAggregator
            .GetEvent<InventoryUpdatedEvent>()
            .Subscribe(async inventory =>
            {
                await SendOrderConfirmationAsync(inventory.OrderId);
            });
    }
}
```

## ベストプラクティス

### ✅ DO

```csharp
// イベントクラスは不変にする
public class UserLoggedInEvent
{
    public int UserId { get; init; }
    public DateTime LoginTime { get; init; }
}

// 購読は必ず Dispose する
var subscription = _eventAggregator
    .GetEvent<MyEvent>()
    .Subscribe(OnEvent);

// 使い終わったら
subscription.Dispose();

// ReactiveUI の WhenActivated を使用
this.WhenActivated(disposables =>
{
    _eventAggregator
        .GetEvent<MyEvent>()
        .Subscribe(OnEvent)
        .DisposeWith(disposables);
});

// 明確なイベント名を使用
public class OrderCreatedEvent { } // Good
public class Event1 { } // Bad
```

### ❌ DON'T

```csharp
// null イベントを発行しない
_eventAggregator.Publish<MyEvent>(null); // NG

// 大きすぎるデータをイベントに含めない
public class HugeDataEvent
{
    public byte[] LargeData { get; set; } // NG: メモリを圧迫
}

// 購読を Dispose せずに放置しない
_eventAggregator.GetEvent<MyEvent>().Subscribe(OnEvent); // NG: メモリリーク

// イベント内でイベントを発行しない（循環参照）
_eventAggregator.GetEvent<Event1>().Subscribe(e =>
{
    _eventAggregator.Publish(new Event1()); // NG: 無限ループ
});
```

## よくある質問

### Q: いつEventAggregatorを使うべき？

**A:**
- ViewModelやサービス間の疎結合な通信
- ドメインイベントの発行
- UIコンポーネント間の通信
- 非同期ワークフローの調整

### Q: 強い型付けされたイベントと文字列ベースのメッセージングの違いは？

**A:**
- **EventAggregator**: 型安全、コンパイル時チェック、IntelliSense対応
- **文字列ベース**: 実行時エラーのリスク、型安全性なし

### Q: パフォーマンスへの影響は？

**A:**
- ReactiveUIのObservableは非常に効率的
- 多数の購読者がいる場合はわずかなオーバーヘッド
- 適切に購読を管理すればメモリリークなし

## 関連モジュール

- [Results](../Results/README.md) - Result<T> パターンの実装
- [Domain](../Domain/README.md) - ドメインイベント

## 参考リンク

- [ReactiveUI Documentation](https://www.reactiveui.net/)
- [Reactive Extensions](http://reactivex.io/)
- [Event Aggregator Pattern](https://martinfowler.com/eaaDev/EventAggregator.html)

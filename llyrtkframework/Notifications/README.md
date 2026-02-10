# Notifications モジュール

アプリケーション内通知システムを提供するモジュールです。

## 概要

Notificationsモジュールは、ReactiveUIベースの通知管理を提供します：

- **型付き通知**: Information、Success、Warning、Error
- **リアクティブ**: ReactiveUIで通知を購読
- **通知キュー**: 自動処理キュー
- **メタデータ**: 任意のデータを通知に添付
- **フィルタリング**: 種類別・状態別のフィルタリング

## 主要コンポーネント

### INotificationService

通知サービスのインターフェース。

```csharp
public interface INotificationService
{
    Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<Result> SendAsync(string title, string message, NotificationType type = NotificationType.Information);
    IObservable<Notification> Notifications { get; }
    IObservable<Notification> GetNotificationsByType(NotificationType type);
}
```

### NotificationService

通知サービスの実装クラス。

```csharp
var service = new NotificationService(logger);

// 通知を送信
await service.SendAsync("成功", "データを保存しました", NotificationType.Success);

// 通知を購読
service.Notifications.Subscribe(notification =>
{
    Console.WriteLine($"{notification.Type}: {notification.Message}");
});
```

### Notification

通知オブジェクト。

```csharp
public class Notification
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string Message { get; init; }
    public NotificationType Type { get; init; }
    public DateTime Timestamp { get; init; }
    public TimeSpan? Duration { get; init; }
    public bool IsRead { get; set; }
    public Dictionary<string, object> Metadata { get; init; }
}
```

### NotificationType

通知の種類。

```csharp
public enum NotificationType
{
    Information,
    Success,
    Warning,
    Error
}
```

### NotificationQueue

通知キューの管理。

```csharp
var queue = new NotificationQueue(service, TimeSpan.FromSeconds(1), logger);

// キューに追加
queue.Enqueue(notification);

// 自動的に1秒ごとに処理される

// 手動で処理
await queue.ProcessQueueAsync();
```

## 使用例

### 基本的な使用方法

```csharp
var service = new NotificationService(logger);

// 簡易版
await service.SendAsync("情報", "処理を開始しました");
await service.SendAsync("成功", "保存完了", NotificationType.Success);
await service.SendAsync("警告", "容量が不足しています", NotificationType.Warning);
await service.SendAsync("エラー", "接続に失敗しました", NotificationType.Error);

// 詳細版
var notification = new Notification
{
    Id = Guid.NewGuid().ToString(),
    Title = "ダウンロード完了",
    Message = "ファイルのダウンロードが完了しました",
    Type = NotificationType.Success,
    Duration = TimeSpan.FromSeconds(5),
    Metadata = new Dictionary<string, object>
    {
        ["FilePath"] = "/downloads/file.txt",
        ["FileSize"] = 1024
    }
};

await service.SendAsync(notification);
```

### 通知の購読

```csharp
var service = new NotificationService(logger);

// すべての通知を購読
service.Notifications.Subscribe(n =>
{
    Console.WriteLine($"[{n.Type}] {n.Title}: {n.Message}");
});

// エラー通知のみ購読
service.Errors().Subscribe(n =>
{
    LogError(n.Message);
    ShowErrorDialog(n.Title, n.Message);
});

// 警告通知のみ購読
service.Warnings().Subscribe(n =>
{
    ShowWarningBanner(n.Message);
});

// 成功通知のみ購読
service.Successes().Subscribe(n =>
{
    ShowSuccessToast(n.Message);
});

// 情報通知のみ購読
service.Informations().Subscribe(n =>
{
    UpdateStatusBar(n.Message);
});
```

### ViewModelでの使用

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly INotificationService _notificationService;
    private readonly ObservableCollection<Notification> _notifications = new();
    private IDisposable? _subscription;

    public ObservableCollection<Notification> Notifications => _notifications;

    public AsyncDelegateCommand SaveCommand { get; }

    public MainViewModel(INotificationService notificationService)
    {
        _notificationService = notificationService;

        SaveCommand = new AsyncDelegateCommand(SaveAsync);

        // 通知を購読してリストに追加
        _subscription = _notificationService.Notifications
            .Subscribe(notification =>
            {
                _notifications.Insert(0, notification);

                // 最大50件まで
                while (_notifications.Count > 50)
                {
                    _notifications.RemoveAt(_notifications.Count - 1);
                }
            });
    }

    private async Task SaveAsync()
    {
        try
        {
            await _notificationService.SendAsync(
                "保存中",
                "データを保存しています...",
                NotificationType.Information
            );

            // 保存処理...
            await Task.Delay(1000);

            await _notificationService.SendAsync(
                "成功",
                "データを保存しました",
                NotificationType.Success
            );
        }
        catch (Exception ex)
        {
            await _notificationService.SendAsync(
                "エラー",
                $"保存に失敗しました: {ex.Message}",
                NotificationType.Error
            );
        }
    }

    public override void OnDeactivated()
    {
        _subscription?.Dispose();
    }
}
```

### 通知の表示コンポーネント

```csharp
public class NotificationPanelViewModel : ViewModelBase
{
    private readonly INotificationService _notificationService;
    private ObservableCollection<Notification> _recentNotifications = new();
    private IDisposable? _subscription;

    public ObservableCollection<Notification> RecentNotifications
    {
        get => _recentNotifications;
        set => this.RaiseAndSetIfChanged(ref _recentNotifications, value);
    }

    public DelegateCommand<Notification> MarkAsReadCommand { get; }
    public DelegateCommand<Notification> DismissCommand { get; }

    public NotificationPanelViewModel(INotificationService notificationService)
    {
        _notificationService = notificationService;

        MarkAsReadCommand = new DelegateCommand<Notification>(notification =>
        {
            if (notification != null)
                notification.IsRead = true;
        });

        DismissCommand = new DelegateCommand<Notification>(notification =>
        {
            if (notification != null)
                RecentNotifications.Remove(notification);
        });

        // 未読の通知のみ表示
        _subscription = _notificationService.Notifications
            .Unread()
            .Subscribe(notification =>
            {
                RecentNotifications.Insert(0, notification);

                // 自動削除（エラー以外は5秒後）
                if (notification.Type != NotificationType.Error)
                {
                    Task.Delay(5000).ContinueWith(_ =>
                    {
                        RecentNotifications.Remove(notification);
                    });
                }
            });
    }

    public override void OnDeactivated()
    {
        _subscription?.Dispose();
    }
}
```

### デバウンス通知

```csharp
public class SearchViewModel : ViewModelBase
{
    private readonly INotificationService _notificationService;
    private IDisposable? _searchSubscription;
    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public SearchViewModel(INotificationService notificationService)
    {
        _notificationService = notificationService;

        // 検索テキスト変更を監視（500msデバウンス）
        _searchSubscription = this.WhenPropertyChangedThrottled(
            vm => vm.SearchText,
            TimeSpan.FromMilliseconds(500),
            async searchText =>
            {
                if (string.IsNullOrEmpty(searchText))
                    return;

                await _notificationService.SendAsync(
                    "検索中",
                    $"'{searchText}' を検索しています...",
                    NotificationType.Information
                );

                // 検索処理...
            }
        );
    }

    public override void OnDeactivated()
    {
        _searchSubscription?.Dispose();
    }
}
```

### 通知キューの使用

```csharp
public class BatchProcessViewModel : ViewModelBase
{
    private readonly NotificationQueue _queue;
    private readonly INotificationService _service;

    public AsyncDelegateCommand ProcessBatchCommand { get; }

    public BatchProcessViewModel(INotificationService service, ILogger logger)
    {
        _service = service;
        _queue = new NotificationQueue(service, TimeSpan.FromSeconds(1), logger);

        ProcessBatchCommand = new AsyncDelegateCommand(ProcessBatchAsync);
    }

    private async Task ProcessBatchAsync()
    {
        var items = Enumerable.Range(1, 100);

        foreach (var item in items)
        {
            // キューに追加（1秒ごとに自動処理）
            _queue.Enqueue(new Notification
            {
                Id = Guid.NewGuid().ToString(),
                Title = "処理中",
                Message = $"Item {item} を処理しました",
                Type = NotificationType.Information
            });

            await Task.Delay(10);  // 処理のシミュレーション
        }

        // 完了通知は即座に送信
        await _service.SendAsync(
            "完了",
            "すべてのアイテムを処理しました",
            NotificationType.Success
        );
    }
}
```

## 拡張メソッド

### Errors / Warnings / Successes / Informations

種類別にフィルタリング。

```csharp
// エラーのみ
service.Errors().Subscribe(error =>
{
    Console.WriteLine($"Error: {error.Message}");
});

// 警告のみ
service.Warnings().Subscribe(warning =>
{
    Console.WriteLine($"Warning: {warning.Message}");
});

// 成功のみ
service.Successes().Subscribe(success =>
{
    Console.WriteLine($"Success: {success.Message}");
});

// 情報のみ
service.Informations().Subscribe(info =>
{
    Console.WriteLine($"Info: {info.Message}");
});
```

### Unread

未読の通知のみ。

```csharp
service.Notifications
    .Unread()
    .Subscribe(notification =>
    {
        ShowNotificationBadge(notification);
    });
```

### Debounce

デバウンス付きで購読。

```csharp
// 1秒以内の連続した通知は最後の1つだけ
service.Notifications
    .Debounce(TimeSpan.FromSeconds(1))
    .Subscribe(notification =>
    {
        UpdateUI(notification);
    });
```

## DI統合

```csharp
// App.xaml.cs
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // Singleton登録
    containerRegistry.RegisterSingleton<INotificationService>(provider =>
    {
        var logger = provider.Resolve<ILogger<NotificationService>>();
        return new NotificationService(logger);
    });

    // 通知キュー（オプション）
    containerRegistry.RegisterSingleton<NotificationQueue>(provider =>
    {
        var service = provider.Resolve<INotificationService>();
        var logger = provider.Resolve<ILogger<NotificationQueue>>();
        return new NotificationQueue(service, TimeSpan.FromSeconds(1), logger);
    });
}
```

## 他サービスとの統合

### Dialogとの統合

```csharp
public class NotificationDialogService
{
    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;

    public NotificationDialogService(
        INotificationService notificationService,
        IDialogService dialogService)
    {
        _notificationService = notificationService;
        _dialogService = dialogService;

        // エラー通知をダイアログで表示
        _notificationService.Errors().Subscribe(async error =>
        {
            await _dialogService.ShowErrorAsync(error.Title, error.Message);
        });
    }
}
```

### Loggingとの統合

```csharp
public class NotificationLoggingService
{
    public NotificationLoggingService(
        INotificationService notificationService,
        ILogger logger)
    {
        // すべての通知をログに記録
        notificationService.Notifications.Subscribe(n =>
        {
            var logLevel = n.Type switch
            {
                NotificationType.Error => LogLevel.Error,
                NotificationType.Warning => LogLevel.Warning,
                NotificationType.Success => LogLevel.Information,
                _ => LogLevel.Debug
            };

            logger.Log(logLevel, "{Title}: {Message}", n.Title, n.Message);
        });
    }
}
```

## ベストプラクティス

1. **Singleton管理**: INotificationServiceはアプリ全体で1インスタンス
2. **購読の解放**: OnDeactivated()でDispose
3. **適切な種類選択**: 通知の重要度で種類を選択
4. **メタデータ活用**: 追加情報はMetadataに格納
5. **デバウンス**: 頻繁な通知はDebounceで間引く

```csharp
// 良い例：重要度に応じた通知
await service.SendAsync("エラー", "データベース接続失敗", NotificationType.Error);
await service.SendAsync("警告", "ディスク容量不足", NotificationType.Warning);
await service.SendAsync("成功", "保存完了", NotificationType.Success);
await service.SendAsync("情報", "処理中...", NotificationType.Information);

// 悪い例：すべてInformation
await service.SendAsync("エラー", "失敗", NotificationType.Information);  // NG
```

## 他モジュールとの統合

- **MVVM**: ViewModelでの通知表示
- **Events**: EventAggregatorとの併用可能
- **Logging**: 通知をログに記録
- **Results**: エラーResultから通知を生成

# Startup モジュール

アプリケーション起動時に実行するタスクの管理と実行を提供するモジュールです。

## 概要

Startupモジュールは、アプリケーション起動時に必要な初期化処理を順序立てて実行するための仕組みを提供します：

- **順序制御**: タスクの実行順序を Order プロパティで指定
- **進捗通知**: 各タスクの実行状況をイベントで通知
- **エラーハンドリング**: タスク失敗時の処理を統一的に管理
- **DI統合**: 依存性注入と統合しやすい設計

## 主要コンポーネント

### IStartupTask

起動時に実行するタスクのインターフェース。

```csharp
public interface IStartupTask
{
    int Order { get; }          // 実行順序（小さいほど先に実行）
    string Name { get; }        // タスク名（ログ・UI表示用）
    Task<Result> ExecuteAsync(CancellationToken cancellationToken = default);
}
```

**Order の推奨値:**
- 10: データ読み込み
- 20: 更新チェック
- 30: 初期化処理
- 40: UI準備

### StartupTaskRunner

複数の IStartupTask を順次実行するランナー。

```csharp
var tasks = new List<IStartupTask>
{
    new DataLoadTask(),
    new UpdateCheckTask(),
    new InitializationTask()
};

var runner = new StartupTaskRunner(tasks, logger);

// 進捗イベントを購読
runner.ProgressChanged += (sender, e) =>
{
    Console.WriteLine($"{e.TaskName} ({e.CurrentTask}/{e.TotalTasks})");
};

// すべてのタスクを実行
var result = await runner.RunAllAsync();
if (result.IsFailure)
{
    Console.WriteLine($"Startup failed: {result.ErrorMessage}");
}
```

### StartupProgressEventArgs

起動タスクの進捗情報を表すイベント引数。

```csharp
public class StartupProgressEventArgs : EventArgs
{
    public string TaskName { get; }         // 現在のタスク名
    public int CurrentTask { get; }         // 現在のタスク番号（1から始まる）
    public int TotalTasks { get; }          // 全タスク数
    public double Progress { get; }         // 進捗率（0.0～1.0）
    public int ProgressPercentage { get; }  // 進捗率（0～100）
}
```

## 使用例

### 基本的なタスクの実装

```csharp
// データ読み込みタスク
public class DataLoadTask : IStartupTask
{
    private readonly FileManagerRegistry _registry;
    private readonly ILogger<DataLoadTask> _logger;

    public int Order => 10;
    public string Name => "データ読み込み";

    public DataLoadTask(FileManagerRegistry registry, ILogger<DataLoadTask> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading data files...");

        var result = await _registry.LoadAllAsync();
        if (result.IsSuccess)
        {
            _registry.StartAutoSave();
            _logger.LogInformation("Data loaded successfully");
        }
        else
        {
            _logger.LogError("Failed to load data: {Error}", result.ErrorMessage);
        }

        return result;
    }
}

// 更新チェックタスク
public class UpdateCheckTask : IStartupTask
{
    private readonly IDataSyncService _syncService;
    private readonly INotificationService _notification;
    private readonly ILogger<UpdateCheckTask> _logger;

    public int Order => 20;
    public string Name => "更新確認";

    public UpdateCheckTask(
        IDataSyncService syncService,
        INotificationService notification,
        ILogger<UpdateCheckTask> logger)
    {
        _syncService = syncService;
        _notification = notification;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates...");

            var hasUpdate = await _syncService.CheckForUpdatesAsync();
            if (hasUpdate)
            {
                _logger.LogInformation("Update available");
                await _notification.SendAsync(
                    "更新があります",
                    "新しいバージョンが利用可能です",
                    NotificationType.Information
                );
            }
            else
            {
                _logger.LogInformation("No updates available");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            // 更新チェック失敗でもアプリ起動は続行
            return Result.Success();
        }
    }
}
```

### アプリケーションでの使用

```csharp
// App.xaml.cs (Avalonia) または App.xaml.cs (WPF)
public partial class App : PrismApplication
{
    protected override async void OnInitialized()
    {
        base.OnInitialized();

        var logger = Container.Resolve<ILogger<App>>();

        try
        {
            // タスクを登録
            var tasks = new List<IStartupTask>
            {
                Container.Resolve<DataLoadTask>(),
                Container.Resolve<UpdateCheckTask>(),
                Container.Resolve<InitializationTask>()
            };

            var runner = new StartupTaskRunner(tasks, Container.Resolve<ILogger<StartupTaskRunner>>());

            // 進捗を表示（スプラッシュ画面など）
            runner.ProgressChanged += (_, e) =>
            {
                logger.LogInformation("起動中: {TaskName} ({Current}/{Total})",
                    e.TaskName, e.CurrentTask, e.TotalTasks);

                // スプラッシュ画面に進捗を表示
                // UpdateSplashScreen(e.TaskName, e.ProgressPercentage);
            };

            // タスクを実行
            var result = await runner.RunAllAsync();

            if (result.IsFailure)
            {
                logger.LogError("Startup failed: {Error}", result.ErrorMessage);

                var notification = Container.Resolve<INotificationService>();
                await notification.SendAsync(
                    "起動エラー",
                    $"アプリケーションの起動に失敗しました: {result.ErrorMessage}",
                    NotificationType.Error
                );

                // 必要に応じてアプリを終了
                Shutdown();
                return;
            }

            logger.LogInformation("Startup completed successfully");

            // メインウィンドウを表示
            Container.Resolve<IRegionManager>().RequestNavigate("MainRegion", "MainView");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during startup");
            Shutdown();
        }
    }
}
```

### DI コンテナ登録

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // タスクを登録
    containerRegistry.Register<IStartupTask, DataLoadTask>();
    containerRegistry.Register<IStartupTask, UpdateCheckTask>();
    containerRegistry.Register<IStartupTask, InitializationTask>();

    // StartupTaskRunner を登録（タスクの自動解決）
    containerRegistry.Register<StartupTaskRunner>(provider =>
    {
        var tasks = provider.Resolve<IEnumerable<IStartupTask>>();
        var logger = provider.Resolve<ILogger<StartupTaskRunner>>();
        return new StartupTaskRunner(tasks, logger);
    });

    // または、個別にタスククラスを登録
    containerRegistry.RegisterSingleton<DataLoadTask>();
    containerRegistry.RegisterSingleton<UpdateCheckTask>();
    containerRegistry.RegisterSingleton<InitializationTask>();
}
```

## 応用例

### 条件付きタスク実行

```csharp
public class ConditionalStartupTask : IStartupTask
{
    private readonly IConfiguration _config;

    public int Order => 30;
    public string Name => "条件付き初期化";

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // 設定に応じてタスクをスキップ
        if (!_config.GetValue<bool>("EnableFeatureX"))
        {
            return Result.Success(); // スキップ
        }

        // 実際の処理
        await InitializeFeatureXAsync();
        return Result.Success();
    }
}
```

### 並列実行可能なタスク

同じ Order を持つタスクを並列実行したい場合は、StartupTaskRunner を拡張するか、
Order の直後に並列実行用のタスクグループを作成します。

```csharp
public class ParallelTaskGroup : IStartupTask
{
    private readonly IEnumerable<IStartupTask> _parallelTasks;

    public int Order => 20;
    public string Name => "並列タスクグループ";

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _parallelTasks.Select(t => t.ExecuteAsync(cancellationToken));
        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => r.IsFailure).ToList();
        if (failures.Any())
        {
            return Result.Failure($"並列タスクの一部が失敗しました: {string.Join(", ", failures.Select(f => f.ErrorMessage))}");
        }

        return Result.Success();
    }
}
```

### 進捗バーの表示

```csharp
// スプラッシュ画面のViewModel
public class SplashScreenViewModel : ViewModelBase
{
    [Reactive] public string CurrentTaskName { get; private set; } = "";
    [Reactive] public int ProgressPercentage { get; private set; } = 0;

    public async Task StartupAsync()
    {
        var runner = _container.Resolve<StartupTaskRunner>();

        runner.ProgressChanged += (_, e) =>
        {
            CurrentTaskName = e.TaskName;
            ProgressPercentage = e.ProgressPercentage;
        };

        var result = await runner.RunAllAsync();

        if (result.IsSuccess)
        {
            // メイン画面に遷移
            _navigationService.Navigate("MainWindow");
        }
        else
        {
            // エラー表示
            ShowError(result.ErrorMessage);
        }
    }
}
```

## ベストプラクティス

### 1. タスクの責務を明確に

各タスクは単一の責務を持つべきです。

```csharp
// 良い例: 単一の責務
public class LoadConfigTask : IStartupTask
{
    public async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        return await _configManager.LoadAsync();
    }
}

// 悪い例: 複数の責務
public class LoadAndValidateAndSyncTask : IStartupTask
{
    public async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        await _configManager.LoadAsync();     // 設定読み込み
        await _validator.ValidateAsync();     // 検証
        await _syncer.SyncAsync();            // 同期
        // 3つの責務が混在
    }
}
```

### 2. エラーハンドリングを適切に

必須タスクと任意タスクを区別します。

```csharp
public class OptionalUpdateCheckTask : IStartupTask
{
    public async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await CheckForUpdatesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed, but continuing startup");
            // 失敗してもアプリ起動は続行
            return Result.Success();
        }
    }
}

public class RequiredDataLoadTask : IStartupTask
{
    public async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        var result = await _repository.LoadAsync();
        if (result.IsFailure)
        {
            // 必須タスクなので失敗を返す
            return result;
        }
        return Result.Success();
    }
}
```

### 3. Order の範囲を管理

Order 値を定数として管理すると、順序の把握が容易になります。

```csharp
public static class StartupTaskOrder
{
    public const int PreInitialization = 0;
    public const int DataLoad = 10;
    public const int UpdateCheck = 20;
    public const int Initialization = 30;
    public const int UiSetup = 40;
    public const int PostInitialization = 50;
}

public class DataLoadTask : IStartupTask
{
    public int Order => StartupTaskOrder.DataLoad;
    public string Name => "データ読み込み";
    // ...
}
```

### 4. ロギングを活用

各タスクで詳細なログを記録します。

```csharp
public class DataLoadTask : IStartupTask
{
    public async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting data load task");
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await _repository.LoadAsync();

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Data load completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data load failed");
            return Result.FromException(ex);
        }
    }
}
```

## 他モジュールとの統合

- **Application**: ApplicationBootstrapper との併用可能
- **FileManagement**: データ読み込みタスクで使用
- **Logging**: 各タスクの実行ログを記録
- **Results**: タスクの実行結果を統一的に表現
- **Prism/DI**: DIコンテナと統合しやすい設計

## Application モジュールとの違い

| 機能 | Startup モジュール | Application モジュール |
|-----|-----------------|---------------------|
| タスク分類 | 単一の IStartupTask | 3フェーズ（PreBoot, CoreInit, UiSetup）|
| 実行順序制御 | Order プロパティ | フェーズ別 |
| 進捗通知 | ProgressChanged イベント | なし |
| 用途 | シンプルな起動処理 | 複雑な起動フロー |

両モジュールは共存可能です。Application モジュールの各フェーズ内で StartupTaskRunner を使用することもできます。

## タイミング図

```
アプリケーション起動
    |
    +-- StartupTaskRunner.RunAllAsync()
        |
        +-- Task 1 (Order: 10) - データ読み込み
        |   |-- ProgressChanged イベント発行
        |   +-- ExecuteAsync() 実行
        |
        +-- Task 2 (Order: 20) - 更新確認
        |   |-- ProgressChanged イベント発行
        |   +-- ExecuteAsync() 実行
        |
        +-- Task 3 (Order: 30) - 初期化処理
        |   |-- ProgressChanged イベント発行
        |   +-- ExecuteAsync() 実行
        |
        +-- Task 4 (Order: 40) - UI準備
            |-- ProgressChanged イベント発行
            +-- ExecuteAsync() 実行

すべて成功
    |
    +-- Result.Success() 返却
    +-- メインウィンドウ表示

タスク失敗時
    |
    +-- Result.Failure() 返却
    +-- エラー処理（通知表示など）
    +-- アプリケーション終了（任意）
```

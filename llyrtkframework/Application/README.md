# Application モジュール

アプリケーションのライフサイクル管理、起動制御、クラッシュリカバリーを提供するモジュールです。

## 概要

Applicationモジュールは、プロダクション環境でのアプリケーション実行に必要な機能を提供します：

- **3フェーズ起動**: Pre-boot → Core Init → UI Setup の段階的起動
- **インスタンス制御**: Mutexによる同一フォルダからの重複起動防止
- **クラッシュリカバリー**: 自動バックアップ復元による障害復旧
- **バージョン管理**: 初回起動・アップグレード検出
- **ライフサイクル管理**: 状態管理とシャットダウン処理

## 主要コンポーネント

### ApplicationBootstrapper

3フェーズ起動フローを管理するブートストラッパー。

```csharp
var bootstrapper = new ApplicationBootstrapper(logger);

// Pre-bootタスク
bootstrapper.AddPreBootTask(new CheckApplicationInstanceTask(instanceManager, logger));
bootstrapper.AddPreBootTask(new CrashRecoveryTask(recoveryManager, logger));
bootstrapper.AddPreBootTask(new VersionCheckTask(versionManager, logger));

// Core Initタスク
bootstrapper.AddCoreInitTask(new InitializeConfigurationTask(configManager, logger));
bootstrapper.AddCoreInitTask(new InitializeDataTask(dataManager, logger));

// UI Setupタスク
bootstrapper.AddUiSetupTask(new SyncGitHubDataTask(gitHubSync, logger));
bootstrapper.AddUiSetupTask(new CheckUpdateTask(updateChecker, logger));

// 起動実行
var result = await bootstrapper.BootstrapAsync();
```

### 起動フェーズ

**Phase 1: Pre-boot**
- アプリケーションインスタンスチェック（Mutex）
- クラッシュ検出とバックアップ復元
- バージョン確認（初回起動・アップグレード）

**Phase 2: Core Init**
- DI コンテナ登録
- 設定ファイル読み込み
- データベース初期化
- ローカルデータ読み込み

**Phase 3: UI Setup**
- GitHub からデータ同期
- アプリケーション更新チェック
- UI 初期化
- メインウィンドウ表示

### ApplicationInstanceManager

同一フォルダからの重複起動を防止（別フォルダからは許可）。

```csharp
var instanceManager = new ApplicationInstanceManager(logger);

// インスタンス取得を試行
var result = instanceManager.TryAcquireInstance();
if (result.IsSuccess && result.Value)
{
    // 起動成功
    Console.WriteLine("Application started");
}
else
{
    // 既に起動している
    Console.WriteLine("Another instance is already running");
    return;
}

// アプリケーション終了時
instanceManager.ReleaseInstance();
instanceManager.Dispose();
```

**仕組み:**
- 実行パスから SHA256 ハッシュを生成
- `Global\LlyrtkFramework_{hash}` という名前のMutexを作成
- 同じフォルダのexeは同じMutex名になるため起動できない
- 別フォルダのexeは異なるMutex名になるため同時起動可能

### CrashRecoveryManager

クラッシュ検出とバックアップからの自動復元。

```csharp
var recoveryManager = new CrashRecoveryManager(logger, applicationDataPath);

// 起動時: 前回クラッシュをチェック
var crashResult = recoveryManager.CheckPreviousCrash();
if (crashResult.IsSuccess && crashResult.Value)
{
    Console.WriteLine("Previous crash detected");

    // 最新のバックアップを取得
    var backupResult = recoveryManager.GetLatestBackup("*.json");
    if (backupResult.IsSuccess && backupResult.Value != null)
    {
        // バックアップから復元
        await recoveryManager.RestoreFromBackupAsync(
            backupResult.Value,
            "data/settings.json"
        );
    }
}

// 起動時: クラッシュフラグを設定
recoveryManager.SetCrashFlag();

// 正常終了時: クラッシュフラグをクリア
recoveryManager.ClearCrashFlag();
```

**リカバリーモードなし:**
- クラッシュ時、次回起動で自動的にバックアップから復元
- 特別なリカバリーモードやUIは不要
- FileManagement の自動バックアップ機能と連携

### ApplicationVersionManager

バージョン管理と初回起動・アップグレード検出。

```csharp
var versionManager = new ApplicationVersionManager(logger, applicationDataPath);

// 現在のバージョンを取得
var currentVersion = versionManager.GetCurrentVersion();
Console.WriteLine($"Version: {currentVersion}");

// 初回起動チェック
var isFirstRunResult = versionManager.IsFirstRun();
if (isFirstRunResult.IsSuccess && isFirstRunResult.Value)
{
    Console.WriteLine("First run detected");
    // 初回起動処理（ただしウェルカムダイアログは不要）
}

// バージョン変更チェック
var isChangedResult = versionManager.IsVersionChanged();
if (isChangedResult.IsSuccess && isChangedResult.Value)
{
    Console.WriteLine("Version upgraded");
    // アップグレード処理
}

// バージョンを保存
versionManager.SaveCurrentVersion();
```

### ApplicationLifecycleManager

アプリケーションのライフサイクル管理。

```csharp
var lifecycleManager = new ApplicationLifecycleManager(logger, recoveryManager);

// 状態変更を監視
lifecycleManager.StateChanged.Subscribe(state =>
{
    Console.WriteLine($"State changed: {state}");
});

// シャットダウンハンドラーを登録
lifecycleManager.RegisterShutdownHandler(
    new SaveFilesShutdownHandler(
        async ct => await fileManager.SaveAllAsync(ct),
        logger
    )
);

lifecycleManager.RegisterShutdownHandler(
    new CloseConnectionsShutdownHandler(
        async ct => await connectionManager.CloseAllAsync(ct),
        logger
    )
);

// アプリケーション状態を変更
lifecycleManager.SetState(ApplicationState.Initializing);
// ... 初期化処理 ...
lifecycleManager.SetState(ApplicationState.Running);

// シャットダウン
await lifecycleManager.ShutdownAsync();
```

### ApplicationInfo

アプリケーション情報の取得。

```csharp
var appInfo = new ApplicationInfo();

Console.WriteLine($"Name: {appInfo.Name}");
Console.WriteLine($"Version: {appInfo.Version}");
Console.WriteLine($"Build Date: {appInfo.BuildDate}");
Console.WriteLine($"Company: {appInfo.Company}");
Console.WriteLine($"Executable: {appInfo.ExecutablePath}");
Console.WriteLine($"Data Path: {appInfo.ApplicationDataPath}");

// カスタム設定で作成
var customInfo = new ApplicationInfo(
    name: "MyApp",
    version: new Version(1, 2, 3),
    company: "My Company",
    copyright: "© 2024 My Company"
);
```

## 使用例

### 基本的なアプリケーション起動フロー

```csharp
public class App : PrismApplication
{
    private ApplicationInstanceManager? _instanceManager;
    private ApplicationLifecycleManager? _lifecycleManager;
    private CrashRecoveryManager? _recoveryManager;

    protected override async void OnInitialized()
    {
        var logger = Container.Resolve<ILogger<App>>();
        var appInfo = new ApplicationInfo();

        try
        {
            // インスタンスマネージャー
            _instanceManager = new ApplicationInstanceManager(
                Container.Resolve<ILogger<ApplicationInstanceManager>>()
            );

            var instanceResult = _instanceManager.TryAcquireInstance();
            if (!instanceResult.IsSuccess || !instanceResult.Value)
            {
                logger.LogWarning("Another instance is already running");
                Shutdown();
                return;
            }

            // リカバリーマネージャー
            _recoveryManager = new CrashRecoveryManager(
                Container.Resolve<ILogger<CrashRecoveryManager>>(),
                appInfo.ApplicationDataPath
            );

            // ライフサイクルマネージャー
            _lifecycleManager = new ApplicationLifecycleManager(
                Container.Resolve<ILogger<ApplicationLifecycleManager>>(),
                _recoveryManager
            );

            // シャットダウンハンドラー登録
            RegisterShutdownHandlers();

            // ブートストラップ実行
            var bootstrapper = CreateBootstrapper();
            var bootstrapResult = await bootstrapper.BootstrapAsync();

            if (bootstrapResult.IsFailure)
            {
                logger.LogError("Bootstrap failed: {Error}", bootstrapResult.ErrorMessage);
                Shutdown();
                return;
            }

            _lifecycleManager.SetState(ApplicationState.Running);

            // メインウィンドウを表示
            Container.Resolve<IRegionManager>().RequestNavigate("ContentRegion", "MainView");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application initialization failed");
            Shutdown();
        }
    }

    private ApplicationBootstrapper CreateBootstrapper()
    {
        var bootstrapper = new ApplicationBootstrapper(
            Container.Resolve<ILogger<ApplicationBootstrapper>>()
        );

        // Pre-boot
        bootstrapper.AddPreBootTask(
            new CheckApplicationInstanceTask(
                _instanceManager!,
                Container.Resolve<ILogger<CheckApplicationInstanceTask>>()
            )
        );

        bootstrapper.AddPreBootTask(
            new CrashRecoveryTask(
                _recoveryManager!,
                Container.Resolve<ILogger<CrashRecoveryTask>>(),
                restoreCallback: async () =>
                {
                    // カスタム復元処理
                    var fileManager = Container.Resolve<IFileManager>();
                    return await fileManager.RestoreWithRollbackAsync("settings.json");
                }
            )
        );

        bootstrapper.AddPreBootTask(
            new VersionCheckTask(
                new ApplicationVersionManager(
                    Container.Resolve<ILogger<ApplicationVersionManager>>(),
                    new ApplicationInfo().ApplicationDataPath
                ),
                Container.Resolve<ILogger<VersionCheckTask>>()
            )
        );

        // Core Init
        bootstrapper.AddCoreInitTask(new InitializeConfigurationTask());
        bootstrapper.AddCoreInitTask(new InitializeDatabaseTask());

        // UI Setup
        bootstrapper.AddUiSetupTask(new GitHubSyncTask());
        bootstrapper.AddUiSetupTask(new CheckUpdateTask());

        return bootstrapper;
    }

    private void RegisterShutdownHandlers()
    {
        // ファイル保存
        _lifecycleManager!.RegisterShutdownHandler(
            new SaveFilesShutdownHandler(
                async ct =>
                {
                    var fileManager = Container.Resolve<IFileManager>();
                    return await fileManager.SaveAllAsync(ct);
                },
                Container.Resolve<ILogger<SaveFilesShutdownHandler>>()
            )
        );
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            // シャットダウン処理
            if (_lifecycleManager != null)
            {
                await _lifecycleManager.ShutdownAsync();
                _lifecycleManager.Dispose();
            }

            _instanceManager?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Shutdown error: {ex.Message}");
        }

        base.OnExit(e);
    }
}
```

### カスタムタスクの実装

**Core Init タスク例:**

```csharp
public class InitializeConfigurationTask : ICoreInitTask
{
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<InitializeConfigurationTask> _logger;

    public InitializeConfigurationTask(
        IConfigurationManager configManager,
        ILogger<InitializeConfigurationTask> logger)
    {
        _configManager = configManager;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing configuration");

        // 設定ファイル読み込み
        var result = await _configManager.LoadAsync("appsettings.json", cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to load configuration: {Error}", result.ErrorMessage);
            return result;
        }

        _logger.LogInformation("Configuration initialized");
        return Result.Success();
    }
}
```

**UI Setup タスク例:**

```csharp
public class GitHubSyncTask : IUiSetupTask
{
    private readonly IFileManager _fileManager;
    private readonly ILogger<GitHubSyncTask> _logger;

    public GitHubSyncTask(
        IFileManager fileManager,
        ILogger<GitHubSyncTask> logger)
    {
        _fileManager = fileManager;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing data from GitHub");

        // GitHub からデータ同期
        var result = await _fileManager.SyncFromGitHubAsync(
            "owner/repo",
            "data/input.json",
            "local/input.json",
            cancellationToken
        );

        if (result.IsFailure)
        {
            _logger.LogWarning("GitHub sync failed: {Error}", result.ErrorMessage);
            // 同期失敗でもアプリ起動は続行
            return Result.Success();
        }

        _logger.LogInformation("GitHub sync completed");
        return Result.Success();
    }
}
```

## クラッシュリカバリーの仕組み

### クラッシュ検出

1. アプリ起動時に `.crash_flag` ファイルを作成
2. 正常終了時に `.crash_flag` を削除
3. 次回起動時に `.crash_flag` が存在すればクラッシュと判断

### 自動復元

1. クラッシュ検出時、`backups` フォルダから最新バックアップを検索
2. バックアップファイルを本来の場所にコピー
3. 破損ファイルは `.corrupted` として保存
4. 通常通りアプリを起動

### FileManagement との統合

```csharp
// FileManager で自動バックアップ
var fileManager = new FileManager(logger, backupTrigger);

// 設定を有効化
fileManager.BackupEnabled = true;

// ファイル保存時に自動バックアップ
await fileManager.SaveAsync("data/settings.json", content);
// -> backups/settings.json.20240101_120000 が作成される

// クラッシュ後、CrashRecoveryManager が自動復元
// -> backups/ から最新ファイルを data/settings.json に復元
```

## DI統合

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    var appInfo = new ApplicationInfo();

    // Singleton登録
    containerRegistry.RegisterSingleton<ApplicationInfo>(() => appInfo);

    containerRegistry.RegisterSingleton<ApplicationInstanceManager>();

    containerRegistry.RegisterSingleton<CrashRecoveryManager>(provider =>
    {
        return new CrashRecoveryManager(
            provider.Resolve<ILogger<CrashRecoveryManager>>(),
            appInfo.ApplicationDataPath
        );
    });

    containerRegistry.RegisterSingleton<ApplicationVersionManager>(provider =>
    {
        return new ApplicationVersionManager(
            provider.Resolve<ILogger<ApplicationVersionManager>>(),
            appInfo.ApplicationDataPath
        );
    });

    containerRegistry.RegisterSingleton<ApplicationLifecycleManager>();

    // ブートストラッパー（必要に応じて）
    containerRegistry.Register<ApplicationBootstrapper>();
}
```

## ベストプラクティス

1. **タスクの責務分離**: 各タスクは単一の責務のみ
2. **エラーハンドリング**: タスク失敗時の挙動を明確に
3. **ログ記録**: 各フェーズで詳細なログを記録
4. **リカバリーモード不要**: 自動バックアップ復元で十分
5. **ウェルカムダイアログ不要**: Infoバーで使い方を表示

```csharp
// 良い例: シンプルなタスク
public class LoadDataTask : ICoreInitTask
{
    public async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        // データ読み込みのみ
        return await _repository.LoadAsync(ct);
    }
}

// 悪い例: 複数の責務
public class LoadAndValidateAndSyncTask : ICoreInitTask
{
    public async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        await _repository.LoadAsync(ct);      // 読み込み
        await _validator.ValidateAsync(ct);   // 検証
        await _syncer.SyncAsync(ct);          // 同期
        // 3つの責務が混在
    }
}
```

## 他モジュールとの統合

- **FileManagement**: 自動バックアップとリカバリー連携
- **Configuration**: アプリ設定の読み込み
- **Logging**: 起動フローの詳細ログ
- **StateManagement**: アプリ状態の永続化
- **Prism**: DIコンテナとの統合

## タイミング図

```
起動フロー:
App.OnInitialized()
    |
    +-- InstanceManager.TryAcquireInstance()
    |   (Mutex取得)
    |
    +-- CrashRecoveryManager.CheckPreviousCrash()
    |   (クラッシュ検出)
    |   |
    |   +-- [クラッシュあり] RestoreFromBackupAsync()
    |       (自動復元)
    |
    +-- VersionManager (初回起動/アップグレード)
    |
    +-- Bootstrapper.BootstrapAsync()
        |
        +-- Phase 1: Pre-boot (100-200ms)
        |   - Mutexチェック
        |   - クラッシュリカバリー
        |   - バージョンチェック
        |
        +-- Phase 2: Core Init (200-500ms)
        |   - DI登録
        |   - 設定読み込み
        |   - データ初期化
        |
        +-- Phase 3: UI Setup (500-1000ms)
            - GitHub同期
            - 更新チェック
            - UI初期化

正常終了:
App.OnExit()
    |
    +-- LifecycleManager.ShutdownAsync()
        |
        +-- SaveFilesShutdownHandler
        |   (ファイル保存)
        |
        +-- CloseConnectionsShutdownHandler
        |   (接続クローズ)
        |
        +-- CrashRecoveryManager.ClearCrashFlag()
            (クラッシュフラグ削除)
```

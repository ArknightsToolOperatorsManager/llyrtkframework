# llyrtkframework

Avalonia + Prism アプリケーション開発のための包括的なフレームワーク

## 概要

llyrtkframework は、.NET 8.0 ベースの Avalonia MVVM アプリケーション開発を加速する、プロダクション対応の統合フレームワークです。

### 主な特徴

- 🚀 **プロダクションレディ**: 実際のアプリケーション開発に必要な機能をすべて搭載
- 🔌 **Prism.Avalonia完全対応**: ワンライナーでDIコンテナに一括登録
- 📦 **モジュラー設計**: 必要な機能だけを選択可能
- 📚 **包括的なドキュメント**: 各モジュールに詳細なREADMEと使用例
- 🎯 **Result<T>パターン**: 一貫したエラーハンドリング
- ⚡ **ReactiveUI統合**: リアクティブプログラミング対応
- 🔒 **セキュリティ**: AES-256-GCM暗号化、DPAPI統合
- 🔄 **GitHub統合**: ファイル同期とアプリ更新チェック

## モジュール一覧

### 基本機能

| モジュール | 説明 | README |
|----------|------|--------|
| **Logging** | Serilog統合ロギング | [README](llyrtkframework/Logging/README.md) |
| **Results** | Result<T>パターン | [README](llyrtkframework/Results/README.md) |
| **Guard** | 引数検証ヘルパー | [README](llyrtkframework/Guard/README.md) |
| **Domain** | Entity, ValueObject | [README](llyrtkframework/Domain/README.md) |
| **Time** | IDateTimeProvider | [README](llyrtkframework/Time/README.md) |
| **Specification** | Specificationパターン | [README](llyrtkframework/Specification/README.md) |
| **Validation** | FluentValidation統合 | [README](llyrtkframework/Validation/README.md) |
| **Resilience** | Polly統合（リトライ、CB） | [README](llyrtkframework/Resilience/README.md) |

### データとキャッシュ

| モジュール | 説明 | README |
|----------|------|--------|
| **Caching** | インメモリキャッシュ | [README](llyrtkframework/Caching/README.md) |
| **Configuration** | 設定管理 | [README](llyrtkframework/Configuration/README.md) |
| **StateManagement** | 状態管理・永続化 | [README](llyrtkframework/StateManagement/README.md) |
| **DataAccess** | Repository, Unit of Work | [README](llyrtkframework/DataAccess/README.md) |
| **FileManagement** | ファイル操作、GitHub同期 | [README](llyrtkframework/FileManagement/README.md) |

### UI/MVVM

| モジュール | 説明 | README |
|----------|------|--------|
| **Mvvm** | ViewModelBase, Commands | [README](llyrtkframework/Mvvm/README.md) |
| **Events** | EventAggregator | [README](llyrtkframework/Events/README.md) |
| **Notifications** | 通知サービス | [README](llyrtkframework/Notifications/README.md) |
| **Localization** | 多言語対応（i18n） | [README](llyrtkframework/Localization/README.md) |
| **Workflow** | Pipelineパターン | [README](llyrtkframework/Workflow/README.md) |

### アプリケーション管理

| モジュール | 説明 | README |
|----------|------|--------|
| **Application** | ライフサイクル、起動制御 | [README](llyrtkframework/Application/README.md) |
| **Security** | 暗号化、ハッシュ、セキュアストレージ | [README](llyrtkframework/Security/README.md) |
| **Diagnostics** | 例外ハンドリング、診断情報 | [README](llyrtkframework/Diagnostics/README.md) |
| **Update** | GitHub Releases更新チェック | [README](llyrtkframework/Update/README.md) |
| **PrismIntegration** | Prism.Avalonia統合 | [README](llyrtkframework/PrismIntegration/README.md) |

## クイックスタート

### インストール

```bash
# プロジェクトをクローン
git clone https://github.com/yourname/llyrtkframework.git

# または NuGet パッケージとして参照
dotnet add reference path/to/llyrtkframework/llyrtkframework.csproj
```

### 基本的な使用方法

```csharp
using llyrtkframework.PrismIntegration;
using Prism.Ioc;

public class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // フレームワーク全体をワンライナーで登録
        containerRegistry.RegisterLlyrtkFramework(options =>
        {
            options.ApplicationInfo = new ApplicationInfo(
                name: "MyApp",
                version: new Version(1, 0, 0)
            );

            options.ConfigureSerilog = config =>
            {
                config
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day);
            };
        });

        // アプリ固有のサービス
        containerRegistry.Register<IDialogService, PrismDialogService>();
        containerRegistry.Register<INavigationService, PrismNavigationService>();
    }
}
```

## ViewModelの例

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly IFileManager _fileManager;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(
        IFileManager fileManager,
        INotificationService notificationService,
        ILogger<MainViewModel> logger)
    {
        _fileManager = fileManager;
        _notificationService = notificationService;
        _logger = logger;

        SaveCommand = new AsyncDelegateCommand(SaveAsync);
    }

    public AsyncDelegateCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;

            var result = await _fileManager.SaveAsync("data.json", _data);
            if (result.IsSuccess)
            {
                await _notificationService.SendAsync(
                    "成功",
                    "保存しました",
                    NotificationType.Success
                );
            }
            else
            {
                _logger.LogError("Save failed: {Error}", result.ErrorMessage);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

## 主要機能

### 3フェーズ起動

アプリケーションの起動を3つのフェーズに分けて管理：

1. **Pre-boot**: Mutexチェック、クラッシュリカバリー、バージョン確認
2. **Core Init**: DI登録、設定読み込み、データ初期化
3. **UI Setup**: GitHub同期、更新チェック、UI初期化

```csharp
var bootstrapper = new ApplicationBootstrapper(logger);

// Pre-boot
bootstrapper.AddPreBootTask(new CheckApplicationInstanceTask(...));
bootstrapper.AddPreBootTask(new CrashRecoveryTask(...));
bootstrapper.AddPreBootTask(new VersionCheckTask(...));

// Core Init
bootstrapper.AddCoreInitTask(new LoadConfigurationTask(...));

// UI Setup
bootstrapper.AddUiSetupTask(new GitHubSyncTask(...));
bootstrapper.AddUiSetupTask(new CheckUpdateTask(...));

await bootstrapper.BootstrapAsync();
```

### ファイル管理とGitHub統合

```csharp
var fileManager = new FileManager(logger, backupTrigger);

// ローカルファイル操作
await fileManager.SaveAsync("data.json", content);
await fileManager.LoadAsync<MyData>("data.json");

// GitHubからダウンロード
await fileManager.DownloadFromGitHubAsync(
    "owner/repo",
    "data/input.json",
    "local/input.json"
);

// 自動同期
var trigger = new GitHubBackupTrigger(
    "owner/repo",
    "data/input.json",
    pollInterval: TimeSpan.FromMinutes(30),
    logger
);
```

### セキュリティ

```csharp
// データ暗号化
var encryptionService = new EncryptionService(logger);
var key = encryptionService.GenerateKey();
var encrypted = encryptionService.EncryptString("secret", key);

// セキュアストレージ（DPAPI）
var secureStorage = new SecureStorage(logger, storagePath);
await secureStorage.SaveAsync("ApiKey", "sk-xxx");
var apiKey = await secureStorage.LoadAsync("ApiKey");
```

### 診断とトラブルシューティング

```csharp
// グローバル例外ハンドラー
var exceptionHandler = new GlobalExceptionHandler(logger, appDataPath);
AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    if (e.ExceptionObject is Exception ex)
    {
        exceptionHandler.HandleExceptionAsync(ex, "AppDomain").Wait();
    }
};

// 診断情報エクスポート
var exporter = new DiagnosticExporter(logger, appDataPath);
await exporter.ExportDiagnosticsAsync("diagnostics.zip");
```

### 更新チェック

```csharp
var updateChecker = new UpdateChecker(logger, httpClient, "owner", "repo");
var result = await updateChecker.CheckForUpdateAsync(currentVersion);

if (result.IsSuccess && result.Value!.IsUpdateAvailable)
{
    Console.WriteLine($"Update available: {result.Value.LatestVersion}");
}
```

## アーキテクチャ

### レイヤー構造

```
┌─────────────────────────────────────────┐
│            Presentation Layer            │
│  (Avalonia Views, ViewModels, Prism)    │
├─────────────────────────────────────────┤
│          Application Layer               │
│  (UseCases, Services, Orchestration)    │
├─────────────────────────────────────────┤
│            Domain Layer                  │
│  (Entities, ValueObjects, Domain Logic) │
├─────────────────────────────────────────┤
│        Infrastructure Layer              │
│  (FileSystem, GitHub, Security, Cache)  │
└─────────────────────────────────────────┘
```

### 依存関係

- **Avalonia UI** 11.x
- **Prism.Avalonia** 9.x
- **ReactiveUI** 22.x
- **DryIoc** (Prismデフォルト)
- **Serilog** 4.x
- **FluentValidation** 12.x
- **Polly** 8.x
- **.NET 8.0**

## ベストプラクティス

### 1. DIコンテナの活用

```csharp
// 良い例: コンストラクタインジェクション
public class MyViewModel : ViewModelBase
{
    public MyViewModel(IFileManager fileManager, ILogger<MyViewModel> logger)
    {
        // DI で自動注入
    }
}

// 悪い例: 直接インスタンス化
public class BadViewModel : ViewModelBase
{
    private readonly FileManager _fileManager = new FileManager(...);  // NG
}
```

### 2. Result<T>パターンの使用

```csharp
// 良い例: Resultでエラーハンドリング
var result = await service.DoSomethingAsync();
if (result.IsFailure)
{
    _logger.LogError("Failed: {Error}", result.ErrorMessage);
    return;
}

// 悪い例: 例外で制御フロー
try
{
    await service.DoSomethingAsync();
}
catch (Exception ex)  // 通常のエラーを例外で処理 NG
{
    // ...
}
```

### 3. ReactiveUIの活用

```csharp
// 良い例: リアクティブプロパティ
private string _searchText = string.Empty;
public string SearchText
{
    get => _searchText;
    set => this.RaiseAndSetIfChanged(ref _searchText, value);
}

// WhenAnyValue で監視
this.WhenAnyValue(x => x.SearchText)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .Subscribe(async text => await SearchAsync(text));
```

## サンプルアプリケーション構成

```
MyApp/
├── App.axaml.cs              # アプリケーションエントリーポイント
├── Views/
│   ├── MainWindow.axaml
│   ├── MainView.axaml
│   └── SettingsView.axaml
├── ViewModels/
│   ├── MainViewModel.cs
│   └── SettingsViewModel.cs
├── Services/
│   └── MyAppService.cs
├── Models/
│   └── MyData.cs
└── Resources/
    ├── en-US.json
    └── ja-JP.json
```

## トラブルシューティング

### ログの確認

```
%LocalAppData%\MyApp\logs\app.log
```

### クラッシュレポート

```
%LocalAppData%\MyApp\crash_reports\
```

### 診断情報のエクスポート

設定画面から「診断情報をエクスポート」を実行すると、すべてのログ・設定・クラッシュレポートがZIPファイルにまとめられます。

## ライセンス

MIT License

## コントリビューション

プルリクエストを歓迎します！

## 関連リンク

- [Avalonia UI](https://avaloniaui.net/)
- [Prism Library](https://prismlibrary.com/)
- [ReactiveUI](https://www.reactiveui.net/)
- [Serilog](https://serilog.net/)

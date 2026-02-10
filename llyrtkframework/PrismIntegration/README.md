# PrismIntegration モジュール

Prism.Avalonia との統合を簡素化するヘルパーモジュールです。

## 概要

PrismIntegrationモジュールは、llyrtkframework の全モジュールを Prism DI コンテナに一括登録する機能を提供します：

- **ワンライナー登録**: すべてのフレームワークコンポーネントを1行で登録
- **カスタマイズ可能**: オプションで動作をカスタマイズ
- **DialogService実装**: Prism の IDialogService を使用
- **NavigationService実装**: Prism の IRegionManager を使用

## 主要コンポーネント

### ContainerRegistration

フレームワーク全体を DI コンテナに登録する拡張メソッド。

```csharp
public class App : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // フレームワーク全体を登録
        containerRegistry.RegisterLlyrtkFramework(options =>
        {
            options.ApplicationInfo = new ApplicationInfo(
                name: "MyApp",
                version: new Version(1, 0, 0),
                company: "My Company"
            );

            options.ConfigurationBasePath = "Config";
            options.DefaultCulture = new CultureInfo("ja-JP");
            options.FileBackupEnabled = true;
            options.FileBackupInterval = TimeSpan.FromHours(1);

            // Serilog 設定
            options.ConfigureSerilog = loggerConfig =>
            {
                loggerConfig
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day);
            };

            // ローカライゼーション初期化
            options.ConfigureLocalization = async service =>
            {
                await service.LoadResourcesFromFileAsync("en-US", "Resources/en-US.json");
                await service.LoadResourcesFromFileAsync("ja-JP", "Resources/ja-JP.json");
            };
        });

        // アプリ固有のサービス登録
        containerRegistry.Register<IDialogService, PrismDialogService>();
        containerRegistry.Register<INavigationService, PrismNavigationService>();

        // ViewModels
        containerRegistry.Register<MainViewModel>();
        containerRegistry.Register<SettingsViewModel>();
    }
}
```

## 登録されるコンポーネント

### 自動登録される項目

| モジュール | 登録内容 |
|-----------|---------|
| **Logging** | ILoggerFactory, ILogger<T> (Serilog) |
| **Time** | IDateTimeProvider |
| **Events** | IEventAggregator |
| **Caching** | ICache |
| **Configuration** | IConfigurationManager |
| **StateManagement** | IStateStore |
| **Localization** | ILocalizationService |
| **Notifications** | INotificationService |
| **Security** | IEncryptionService, IHashService, ISecureStorage |
| **FileManagement** | IFileManager, IBackupTrigger |
| **DataAccess** | IRepository<T> (オプション) |
| **Application** | ApplicationInfo, ApplicationInstanceManager, CrashRecoveryManager, ApplicationVersionManager, ApplicationLifecycleManager, ApplicationBootstrapper |

### 手動登録が必要な項目

- **IDialogService**: アプリで実装を選択
- **INavigationService**: アプリで実装を選択
- **ViewModels**: アプリ固有の ViewModel
- **Views**: Prism の自動検出機能を使用

## PrismDialogService

Prism の IDialogService を使用したダイアログサービス実装。

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;

    public MainViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task ShowErrorAsync()
    {
        await _dialogService.ShowErrorAsync(
            "エラー",
            "処理中にエラーが発生しました"
        );
    }

    public async Task DeleteItemAsync()
    {
        var result = await _dialogService.ShowConfirmationAsync(
            "確認",
            "このアイテムを削除しますか？"
        );

        if (result.IsSuccess && result.Value)
        {
            // 削除処理
        }
    }

    public async Task GetUserInputAsync()
    {
        var result = await _dialogService.ShowInputAsync(
            "名前入力",
            "名前を入力してください",
            defaultValue: "Taro"
        );

        if (result.IsSuccess && result.Value != null)
        {
            var name = result.Value;
            // 名前を使用
        }
    }
}
```

### 必要なダイアログ View の実装

```csharp
// ConfirmationDialog.axaml.cs
public partial class ConfirmationDialog : UserControl, IDialogAware
{
    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    public string Title => "Confirmation";

    public event Action<IDialogResult> RequestClose;

    public bool CanCloseDialog() => true;

    public void OnDialogClosed() { }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        var title = parameters.GetValue<string>("title");
        var message = parameters.GetValue<string>("message");

        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
    }
}
```

## PrismNavigationService

Prism の IRegionManager を使用したナビゲーションサービス実装。

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public async Task NavigateToSettingsAsync()
    {
        var result = await _navigationService.NavigateAsync("SettingsView");
        if (result.IsFailure)
        {
            // エラーハンドリング
        }
    }

    public async Task NavigateWithParameterAsync()
    {
        var parameter = new { UserId = 123 };
        await _navigationService.NavigateAsync("UserDetailView", parameter);
    }

    public async Task GoBackAsync()
    {
        var canGoBack = await _navigationService.CanGoBackAsync();
        if (canGoBack.IsSuccess && canGoBack.Value)
        {
            await _navigationService.GoBackAsync();
        }
    }
}
```

### Region の定義

```xml
<!-- MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:prism="http://prismlibrary.com/"
        x:Class="MyApp.Views.MainWindow">
    <DockPanel>
        <Menu DockPanel.Dock="Top">
            <!-- メニューバー -->
        </Menu>

        <!-- メインコンテンツ領域 -->
        <ContentControl prism:RegionManager.RegionName="ContentRegion" />
    </DockPanel>
</Window>
```

## 使用例

### 完全なアプリケーション起動フロー

```csharp
public class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // フレームワーク全体を登録
        containerRegistry.RegisterLlyrtkFramework(options =>
        {
            options.ApplicationInfo = new ApplicationInfo(
                name: "MyApp",
                version: new Version(1, 2, 3)
            );

            options.ConfigureSerilog = config =>
            {
                config
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .WriteTo.Console()
                    .WriteTo.File(
                        Path.Combine(options.ApplicationDataPath, "logs", "app.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30
                    );
            };

            options.ConfigureLocalization = async service =>
            {
                await service.LoadResourcesFromFileAsync("en-US", "Resources/en-US.json");
                await service.LoadResourcesFromFileAsync("ja-JP", "Resources/ja-JP.json");
                service.SetCulture("ja-JP");
            };
        });

        // MVVM サービス
        containerRegistry.RegisterSingleton<IDialogService, PrismDialogService>();
        containerRegistry.RegisterSingleton<INavigationService>(provider =>
        {
            var regionManager = provider.Resolve<IRegionManager>();
            var logger = provider.Resolve<ILogger<PrismNavigationService>>();
            return new PrismNavigationService(regionManager, logger, "ContentRegion");
        });

        // Views & ViewModels
        containerRegistry.RegisterForNavigation<MainView, MainViewModel>();
        containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
        containerRegistry.RegisterForNavigation<UserDetailView, UserDetailViewModel>();

        // Dialogs
        containerRegistry.RegisterDialog<ConfirmationDialog>();
        containerRegistry.RegisterDialog<ErrorDialog>();
        containerRegistry.RegisterDialog<InformationDialog>();
        containerRegistry.RegisterDialog<WarningDialog>();
        containerRegistry.RegisterDialog<InputDialog>();
        containerRegistry.RegisterDialog<SelectionDialog>();
    }

    protected override async void OnInitialized()
    {
        var logger = Container.Resolve<ILogger<App>>();

        try
        {
            // インスタンス管理
            var instanceManager = Container.Resolve<ApplicationInstanceManager>();
            var instanceResult = instanceManager.TryAcquireInstance();

            if (!instanceResult.IsSuccess || !instanceResult.Value)
            {
                logger.LogWarning("Another instance is already running");
                Shutdown();
                return;
            }

            // ブートストラップ
            var bootstrapper = CreateBootstrapper();
            var bootstrapResult = await bootstrapper.BootstrapAsync();

            if (bootstrapResult.IsFailure)
            {
                logger.LogError("Bootstrap failed: {Error}", bootstrapResult.ErrorMessage);
                Shutdown();
                return;
            }

            // ライフサイクル管理
            var lifecycleManager = Container.Resolve<ApplicationLifecycleManager>();
            lifecycleManager.SetState(ApplicationState.Running);

            // メインビューにナビゲート
            var regionManager = Container.Resolve<IRegionManager>();
            regionManager.RequestNavigate("ContentRegion", "MainView");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application initialization failed");
            Shutdown();
        }
    }

    private ApplicationBootstrapper CreateBootstrapper()
    {
        var bootstrapper = Container.Resolve<ApplicationBootstrapper>();

        // Pre-boot
        bootstrapper.AddPreBootTask(Container.Resolve<CheckApplicationInstanceTask>());
        bootstrapper.AddPreBootTask(Container.Resolve<CrashRecoveryTask>());
        bootstrapper.AddPreBootTask(Container.Resolve<VersionCheckTask>());

        // Core Init
        bootstrapper.AddCoreInitTask(new LoadConfigurationTask(
            Container.Resolve<IConfigurationManager>(),
            Container.Resolve<ILogger<LoadConfigurationTask>>()
        ));

        // UI Setup
        bootstrapper.AddUiSetupTask(new GitHubSyncTask(
            Container.Resolve<IFileManager>(),
            Container.Resolve<ILogger<GitHubSyncTask>>()
        ));

        return bootstrapper;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            var lifecycleManager = Container.Resolve<ApplicationLifecycleManager>();
            await lifecycleManager.ShutdownAsync();

            var instanceManager = Container.Resolve<ApplicationInstanceManager>();
            instanceManager.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Shutdown error: {ex.Message}");
        }

        base.OnExit(e);
    }
}
```

### ViewModel での DI 使用

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly IFileManager _fileManager;
    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _localizationService;
    private readonly IStateStore _stateStore;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(
        IFileManager fileManager,
        INotificationService notificationService,
        IDialogService dialogService,
        INavigationService navigationService,
        ILocalizationService localizationService,
        IStateStore stateStore,
        ILogger<MainViewModel> logger)
    {
        _fileManager = fileManager;
        _notificationService = notificationService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _localizationService = localizationService;
        _stateStore = stateStore;
        _logger = logger;

        SaveCommand = new AsyncDelegateCommand(SaveAsync);
        DeleteCommand = new AsyncDelegateCommand<Item>(DeleteAsync);
        NavigateCommand = new AsyncDelegateCommand<string>(NavigateAsync);
    }

    public AsyncDelegateCommand SaveCommand { get; }
    public AsyncDelegateCommand<Item> DeleteCommand { get; }
    public AsyncDelegateCommand<string> NavigateCommand { get; }

    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;

            var result = await _fileManager.SaveAsync("data.json", _data);
            if (result.IsSuccess)
            {
                await _notificationService.SendAsync(
                    _localizationService.GetString("SaveSuccess").Value ?? "保存しました",
                    NotificationType.Success
                );
            }
            else
            {
                await _dialogService.ShowErrorAsync("エラー", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save failed");
            await _dialogService.ShowErrorAsync("エラー", "保存に失敗しました");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync(Item? item)
    {
        if (item == null) return;

        var confirmResult = await _dialogService.ShowConfirmationAsync(
            "確認",
            $"{item.Name} を削除しますか？"
        );

        if (confirmResult.IsSuccess && confirmResult.Value)
        {
            // 削除処理
            Items.Remove(item);

            await _notificationService.SendAsync(
                "削除しました",
                NotificationType.Success
            );
        }
    }

    private async Task NavigateAsync(string? viewName)
    {
        if (string.IsNullOrEmpty(viewName)) return;

        var result = await _navigationService.NavigateAsync(viewName);
        if (result.IsFailure)
        {
            _logger.LogWarning("Navigation failed: {Error}", result.ErrorMessage);
        }
    }

    public override void OnInitialize()
    {
        // 状態復元
        var stateResult = _stateStore.GetState<MyState>("MainViewState");
        if (stateResult.IsSuccess && stateResult.Value != null)
        {
            RestoreState(stateResult.Value);
        }
    }

    public override void OnDeactivated()
    {
        // 状態保存
        var state = CreateState();
        _stateStore.SetState("MainViewState", state);
    }
}
```

## RegistrationOptions 詳細

```csharp
public class FrameworkRegistrationOptions
{
    // アプリケーション情報
    public ApplicationInfo ApplicationInfo { get; set; }

    // アプリケーションデータディレクトリ
    // デフォルト: %LocalAppData%\{ApplicationName}
    public string ApplicationDataPath { get; }

    // 設定ファイルベースパス
    // デフォルト: "Config"
    public string ConfigurationBasePath { get; set; }

    // デフォルトカルチャ
    // デフォルト: CurrentCulture
    public CultureInfo DefaultCulture { get; set; }

    // ローカライゼーション初期化
    public Action<LocalizationService>? ConfigureLocalization { get; set; }

    // Serilog 設定
    public Action<LoggerConfiguration>? ConfigureSerilog { get; set; }

    // ファイルバックアップ有効化
    // デフォルト: true
    public bool FileBackupEnabled { get; set; }

    // ファイルバックアップ間隔
    // デフォルト: 1時間
    public TimeSpan FileBackupInterval { get; set; }

    // DataAccess (Repository) 登録
    // デフォルト: true
    public bool RegisterDataAccess { get; set; }
}
```

## ベストプラクティス

1. **ワンライナー登録**: `RegisterLlyrtkFramework` でフレームワーク全体を登録
2. **オプションカスタマイズ**: アプリ要件に応じてオプション設定
3. **MVVM サービス登録**: IDialogService, INavigationService は必ず実装を登録
4. **ライフサイクル管理**: OnInitialized / OnExit で適切に初期化・クリーンアップ
5. **DI 活用**: ViewModel のコンストラクタインジェクションを活用

```csharp
// 良い例: すべて DI で解決
public class GoodViewModel : ViewModelBase
{
    public GoodViewModel(
        IFileManager fileManager,
        INotificationService notificationService,
        ILogger<GoodViewModel> logger)
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

## 他モジュールとの統合

- **すべてのフレームワークモジュール**: 自動的に DI 登録
- **Prism.Avalonia**: Region, Dialog, Navigation の統合
- **DryIoc**: Prism のデフォルト DI コンテナ
- **Application**: 起動フロー管理の統合

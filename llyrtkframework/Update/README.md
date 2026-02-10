# Update モジュール

GitHub Releasesからのアプリケーション更新チェック機能を提供するモジュールです。

## 概要

Updateモジュールは、アプリケーションの自動更新をサポートします：

- **GitHub Releases統合**: GitHubの最新リリースを自動取得
- **バージョン比較**: セマンティックバージョニング対応
- **リリースノート取得**: 更新内容の表示
- **アセット情報**: ダウンロード可能なファイル一覧
- **UI Setup統合**: 起動時の自動チェック

## アーキテクチャ

### 更新の流れ

```
1. メインアプリ起動
   ↓
2. UpdateChecker が GitHub API を呼び出し
   ↓
3. 最新バージョンを取得して比較
   ↓
4. 更新があれば通知エリアに表示
   ↓
5. ユーザーがダウンロードページを開く
   ↓
6. 別途用意した Updater.exe で実際の更新
   (フレームワークの範囲外)
```

**重要**: このモジュールは更新チェックのみを提供します。実際のダウンロードと更新は、別途 Updater.exe などの更新プログラムで実装してください。

## 主要コンポーネント

### UpdateChecker

GitHub Releasesから更新情報を取得。

```csharp
var httpClient = new HttpClient();
var updateChecker = new UpdateChecker(
    logger,
    httpClient,
    owner: "yourname",      // GitHub ユーザー名
    repository: "yourapp"   // リポジトリ名
);

// 現在のバージョン
var currentVersion = new Version(1, 2, 3);

// 更新をチェック
var result = await updateChecker.CheckForUpdateAsync(currentVersion);
if (result.IsSuccess)
{
    var updateInfo = result.Value!;

    if (updateInfo.IsUpdateAvailable)
    {
        Console.WriteLine($"Update available: {updateInfo.LatestVersion}");
        Console.WriteLine($"Release: {updateInfo.ReleaseName}");
        Console.WriteLine($"Notes: {updateInfo.ReleaseNotes}");
        Console.WriteLine($"URL: {updateInfo.DownloadUrl}");
    }
    else
    {
        Console.WriteLine("Application is up to date");
    }
}
```

### UpdateInfo

更新情報を保持するクラス。

```csharp
public class UpdateInfo
{
    // 更新が利用可能か
    public bool IsUpdateAvailable { get; set; }

    // 現在のバージョン
    public Version CurrentVersion { get; set; }

    // 最新バージョン
    public Version LatestVersion { get; set; }

    // リリース名
    public string ReleaseName { get; set; }

    // リリースノート（Markdown）
    public string ReleaseNotes { get; set; }

    // 公開日時
    public DateTime PublishedAt { get; set; }

    // GitHubリリースページのURL
    public string DownloadUrl { get; set; }

    // リリースアセット（ダウンロード可能なファイル）
    public List<ReleaseAsset> Assets { get; set; }
}
```

### CheckUpdateTask

起動時の自動更新チェック（UI Setupタスク）。

```csharp
var bootstrapper = new ApplicationBootstrapper(logger);

// UI Setupフェーズで更新チェック
bootstrapper.AddUiSetupTask(new CheckUpdateTask(
    updateChecker,
    currentVersion: new Version(1, 2, 3),
    logger,
    notificationService  // 更新があれば通知
));

await bootstrapper.BootstrapAsync();
```

## 使用例

### 基本的な更新チェック

```csharp
public class UpdateService
{
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(IUpdateChecker updateChecker, ILogger<UpdateService> logger)
    {
        _updateChecker = updateChecker;
        _logger = logger;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        var currentVersion = GetCurrentVersion();

        var result = await _updateChecker.CheckForUpdateAsync(currentVersion);
        if (result.IsFailure)
        {
            _logger.LogError("Update check failed: {Error}", result.ErrorMessage);
            return null;
        }

        return result.Value;
    }

    private Version GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        return assembly?.GetName().Version ?? new Version(1, 0, 0);
    }
}
```

### ViewModelでの使用

```csharp
public class SettingsViewModel : ViewModelBase
{
    private readonly IUpdateChecker _updateChecker;
    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;
    private UpdateInfo? _latestUpdateInfo;

    public AsyncDelegateCommand CheckUpdateCommand { get; }
    public AsyncDelegateCommand OpenDownloadPageCommand { get; }

    public SettingsViewModel(
        IUpdateChecker updateChecker,
        INotificationService notificationService,
        IDialogService dialogService)
    {
        _updateChecker = updateChecker;
        _notificationService = notificationService;
        _dialogService = dialogService;

        CheckUpdateCommand = new AsyncDelegateCommand(CheckUpdateAsync);
        OpenDownloadPageCommand = new AsyncDelegateCommand(
            OpenDownloadPageAsync,
            () => _latestUpdateInfo?.IsUpdateAvailable == true
        );
    }

    private async Task CheckUpdateAsync()
    {
        try
        {
            IsBusy = true;

            var currentVersion = GetCurrentVersion();
            var result = await _updateChecker.CheckForUpdateAsync(currentVersion);

            if (result.IsSuccess)
            {
                _latestUpdateInfo = result.Value!;

                if (_latestUpdateInfo.IsUpdateAvailable)
                {
                    await _dialogService.ShowInformationAsync(
                        "更新があります",
                        $"バージョン {_latestUpdateInfo.LatestVersion} が利用可能です\n\n" +
                        $"{_latestUpdateInfo.ReleaseName}\n\n" +
                        $"{_latestUpdateInfo.ReleaseNotes}"
                    );
                }
                else
                {
                    await _notificationService.SendAsync(
                        "最新版です",
                        "アプリケーションは最新版です",
                        NotificationType.Success
                    );
                }

                OpenDownloadPageCommand.RaiseCanExecuteChanged();
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    "エラー",
                    $"更新チェックに失敗しました: {result.ErrorMessage}"
                );
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenDownloadPageAsync()
    {
        if (_latestUpdateInfo == null) return;

        // ブラウザでGitHubリリースページを開く
        var url = _latestUpdateInfo.DownloadUrl;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private Version GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        return assembly?.GetName().Version ?? new Version(1, 0, 0);
    }
}
```

### 更新通知バナー

```csharp
public class UpdateNotificationViewModel : ViewModelBase
{
    private readonly IUpdateChecker _updateChecker;
    private bool _isUpdateAvailable;
    private UpdateInfo? _updateInfo;

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value);
    }

    public UpdateInfo? UpdateInfo
    {
        get => _updateInfo;
        set => this.RaiseAndSetIfChanged(ref _updateInfo, value);
    }

    public AsyncDelegateCommand CheckUpdateCommand { get; }
    public DelegateCommand OpenDownloadPageCommand { get; }
    public DelegateCommand DismissCommand { get; }

    public UpdateNotificationViewModel(IUpdateChecker updateChecker)
    {
        _updateChecker = updateChecker;

        CheckUpdateCommand = new AsyncDelegateCommand(CheckUpdateAsync);
        OpenDownloadPageCommand = new DelegateCommand(OpenDownloadPage);
        DismissCommand = new DelegateCommand(() => IsUpdateAvailable = false);
    }

    public override void OnInitialize()
    {
        // 起動時に自動チェック
        CheckUpdateCommand.ExecuteAsync();
    }

    private async Task CheckUpdateAsync()
    {
        var currentVersion = GetCurrentVersion();
        var result = await _updateChecker.CheckForUpdateAsync(currentVersion);

        if (result.IsSuccess)
        {
            UpdateInfo = result.Value!;
            IsUpdateAvailable = UpdateInfo.IsUpdateAvailable;
        }
    }

    private void OpenDownloadPage()
    {
        if (UpdateInfo == null) return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = UpdateInfo.DownloadUrl,
            UseShellExecute = true
        });
    }

    private Version GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        return assembly?.GetName().Version ?? new Version(1, 0, 0);
    }
}
```

### XAML (Avalonia)

```xml
<!-- 更新通知バナー -->
<Border IsVisible="{Binding IsUpdateAvailable}"
        Background="#FFF4E5"
        BorderBrush="#FFA500"
        BorderThickness="0,0,0,2"
        Padding="12">
    <Grid ColumnDefinitions="Auto,*,Auto,Auto">
        <!-- アイコン -->
        <TextBlock Grid.Column="0"
                   Text="ℹ️"
                   FontSize="20"
                   VerticalAlignment="Center"
                   Margin="0,0,12,0" />

        <!-- メッセージ -->
        <StackPanel Grid.Column="1" VerticalAlignment="Center">
            <TextBlock Text="{Binding UpdateInfo.ReleaseName, StringFormat='更新: {0}'}"
                       FontWeight="Bold" />
            <TextBlock Text="{Binding UpdateInfo.LatestVersion, StringFormat='バージョン {0} が利用可能です'}"
                       FontSize="12"
                       Foreground="#666" />
        </StackPanel>

        <!-- ダウンロードボタン -->
        <Button Grid.Column="2"
                Content="ダウンロード"
                Command="{Binding OpenDownloadPageCommand}"
                Margin="12,0" />

        <!-- 閉じるボタン -->
        <Button Grid.Column="3"
                Content="×"
                Command="{Binding DismissCommand}"
                FontSize="20"
                Background="Transparent" />
    </Grid>
</Border>
```

## すべてのリリース取得

```csharp
public class ReleaseHistoryViewModel : ViewModelBase
{
    private readonly IUpdateChecker _updateChecker;
    private ObservableCollection<UpdateInfo> _releases = new();

    public ObservableCollection<UpdateInfo> Releases
    {
        get => _releases;
        set => this.RaiseAndSetIfChanged(ref _releases, value);
    }

    public AsyncDelegateCommand LoadReleasesCommand { get; }

    public ReleaseHistoryViewModel(IUpdateChecker updateChecker)
    {
        _updateChecker = updateChecker;
        LoadReleasesCommand = new AsyncDelegateCommand(LoadReleasesAsync);
    }

    public override void OnInitialize()
    {
        LoadReleasesCommand.ExecuteAsync();
    }

    private async Task LoadReleasesAsync()
    {
        try
        {
            IsBusy = true;

            var result = await _updateChecker.GetAllReleasesAsync();
            if (result.IsSuccess)
            {
                Releases = new ObservableCollection<UpdateInfo>(result.Value!);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

## DI統合

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // HttpClient
    containerRegistry.RegisterSingleton<HttpClient>();

    // UpdateChecker
    containerRegistry.RegisterSingleton<IUpdateChecker>(provider =>
    {
        var logger = provider.Resolve<ILogger<UpdateChecker>>();
        var httpClient = provider.Resolve<HttpClient>();

        return new UpdateChecker(
            logger,
            httpClient,
            owner: "yourname",     // 実際のGitHubユーザー名
            repository: "yourapp"  // 実際のリポジトリ名
        );
    });
}
```

## 起動時の自動チェック

```csharp
public class App : PrismApplication
{
    protected override async void OnInitialized()
    {
        // ... 初期化処理 ...

        // ブートストラッパーに更新チェックを追加
        var bootstrapper = Container.Resolve<ApplicationBootstrapper>();

        bootstrapper.AddUiSetupTask(new CheckUpdateTask(
            Container.Resolve<IUpdateChecker>(),
            currentVersion: new ApplicationInfo().Version,
            Container.Resolve<ILogger<CheckUpdateTask>>(),
            Container.Resolve<INotificationService>()
        ));

        var result = await bootstrapper.BootstrapAsync();
        if (result.IsFailure)
        {
            Shutdown();
            return;
        }

        // メインウィンドウ表示
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RequestNavigate("ContentRegion", "MainView");
    }
}
```

## GitHub Releases の設定

### リリースの作成

1. GitHubでタグを作成: `v1.2.3`
2. リリースを作成
3. リリースノート（Markdown）を記述
4. ビルドしたアプリケーションをアセットとして添付

### タグ命名規則

- `v1.0.0` - メジャーバージョン
- `v1.2.0` - マイナーバージョン
- `v1.2.3` - パッチバージョン

UpdateCheckerは `v` プレフィックスを自動的に削除してバージョン比較します。

## ベストプラクティス

1. **起動時チェック**: UI Setupフェーズで自動チェック
2. **エラー処理**: ネットワークエラーでもアプリは続行
3. **通知表示**: 更新があれば控えめに通知
4. **手動チェック**: 設定画面から手動チェック可能に
5. **リリースノート**: 更新内容を明確に記載

```csharp
// 良い例: エラーハンドリング
var result = await updateChecker.CheckForUpdateAsync(currentVersion);
if (result.IsFailure)
{
    _logger.LogWarning("Update check failed: {Error}", result.ErrorMessage);
    // エラーがあってもアプリは続行
    return;
}

// 悪い例: 例外を投げる
try
{
    var updateInfo = await updateChecker.CheckForUpdateAsync(currentVersion);
}
catch
{
    throw; // アプリがクラッシュ NG!
}
```

## セキュリティ考慮事項

- **HTTPS必須**: GitHub APIはHTTPSのみ
- **署名検証**: ダウンロードしたファイルの署名を検証（Updater.exeで実装）
- **User-Agent**: GitHub APIはUser-Agentヘッダーを要求

## 制限事項

- **レート制限**: GitHub APIは認証なしで1時間60リクエストまで
- **チェックのみ**: 実際のダウンロード・更新は別プログラムで実装
- **Windows専用**: 更新の仕組みはOS依存

## 他モジュールとの統合

- **Application**: UI Setupタスクとして統合
- **Notifications**: 更新通知
- **MVVM**: ViewModelで更新チェック
- **Logging**: 更新チェックのログ記録

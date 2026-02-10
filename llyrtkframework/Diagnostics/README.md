# Diagnostics モジュール

グローバル例外ハンドリングと診断情報エクスポートを提供するモジュールです。

## 概要

Diagnosticsモジュールは、プロダクション環境でのトラブルシューティングを支援します：

- **グローバル例外ハンドラー**: キャッチされない例外を処理
- **クラッシュレポート**: 詳細な例外情報を自動保存
- **診断情報エクスポート**: ログ・設定・クラッシュレポートをZIPで一括エクスポート
- **カスタムハンドラー**: 独自の例外処理を追加可能

## 主要コンポーネント

### GlobalExceptionHandler

キャッチされない例外を処理し、クラッシュレポートを生成。

```csharp
var appInfo = new ApplicationInfo();
var exceptionHandler = new GlobalExceptionHandler(logger, appInfo.ApplicationDataPath);

// 例外ハンドラーを登録
exceptionHandler.RegisterHandler(new FileExceptionHandler(
    fileLogger,
    Path.Combine(appInfo.ApplicationDataPath, "exceptions.log")
));

// グローバル例外を処理
try
{
    // アプリケーションコード
}
catch (Exception ex)
{
    await exceptionHandler.HandleExceptionAsync(ex, "MainWindow");
}
```

### アプリケーション全体の例外ハンドリング

```csharp
public class App : PrismApplication
{
    private GlobalExceptionHandler? _exceptionHandler;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // グローバル例外ハンドラーをセットアップ
        _exceptionHandler = Container.Resolve<GlobalExceptionHandler>();

        // AppDomain の未処理例外
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // ディスパッチャーの未処理例外
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Task の未処理例外
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _exceptionHandler?.HandleExceptionAsync(exception, "AppDomain").Wait();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _exceptionHandler?.HandleExceptionAsync(e.Exception, "Dispatcher").Wait();
        e.Handled = true; // 例外を処理済みとしてアプリを続行
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _exceptionHandler?.HandleExceptionAsync(e.Exception, "Task").Wait();
        e.SetObserved(); // 例外を処理済みとする
    }
}
```

## クラッシュレポート

### 自動生成

例外が発生すると、以下の情報を含むクラッシュレポートが自動生成されます：

```json
{
  "Timestamp": "2024-01-15T10:30:45.123Z",
  "Context": "MainWindow",
  "ExceptionType": "System.NullReferenceException",
  "Message": "Object reference not set to an instance of an object.",
  "StackTrace": "at MyApp.ViewModels.MainViewModel.LoadData() ...",
  "InnerException": null,
  "SystemInfo": {
    "OSVersion": "Microsoft Windows NT 10.0.22631.0",
    "OSArchitecture": "x64",
    "ProcessorCount": 8,
    "MachineName": "DESKTOP-ABC123",
    "UserName": "user",
    "CLRVersion": "8.0.0",
    "WorkingSet": 52428800,
    "SystemDirectory": "C:\\Windows\\system32"
  },
  "ApplicationInfo": {
    "Name": "MyApp",
    "Version": "1.2.3.0",
    "Location": "C:\\Program Files\\MyApp\\MyApp.exe",
    "ProcessId": 12345
  }
}
```

### クラッシュレポートの管理

```csharp
var exceptionHandler = new GlobalExceptionHandler(logger, appDataPath);

// すべてのクラッシュレポートを取得
var reportsResult = exceptionHandler.GetCrashReports();
if (reportsResult.IsSuccess)
{
    foreach (var reportPath in reportsResult.Value!)
    {
        Console.WriteLine($"Crash report: {reportPath}");

        // レポートを読み込み
        var reportResult = await exceptionHandler.LoadCrashReportAsync(reportPath);
        if (reportResult.IsSuccess)
        {
            var report = reportResult.Value!;
            Console.WriteLine($"  Timestamp: {report.Timestamp}");
            Console.WriteLine($"  Exception: {report.ExceptionType}");
            Console.WriteLine($"  Message: {report.Message}");
        }
    }
}
```

### 古いレポートの自動削除

クラッシュレポートは30日以上経過すると自動削除されます。

## カスタム例外ハンドラー

### IExceptionHandler の実装

```csharp
public class EmailExceptionHandler : IExceptionHandler
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailExceptionHandler> _logger;

    public EmailExceptionHandler(
        IEmailService emailService,
        ILogger<EmailExceptionHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        Exception exception,
        string context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subject = $"Application Error: {exception.GetType().Name}";
            var body = $@"
Context: {context}
Message: {exception.Message}
Stack Trace:
{exception.StackTrace}
";

            await _emailService.SendAsync("admin@example.com", subject, body, cancellationToken);

            _logger.LogInformation("Exception report sent via email");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send exception email");
            return Result.FromException(ex, "Failed to send exception email");
        }
    }
}

// 登録
exceptionHandler.RegisterHandler(new EmailExceptionHandler(emailService, logger));
```

### 通知サービス連携

```csharp
public class NotificationExceptionHandler : IExceptionHandler
{
    private readonly INotificationService _notificationService;

    public NotificationExceptionHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<Result> HandleAsync(
        Exception exception,
        string context,
        CancellationToken cancellationToken = default)
    {
        await _notificationService.SendAsync(
            "エラー",
            $"エラーが発生しました: {exception.Message}",
            NotificationType.Error
        );

        return Result.Success();
    }
}
```

## DiagnosticExporter

診断情報を ZIP ファイルにエクスポート。

```csharp
var appInfo = new ApplicationInfo();
var exporter = new DiagnosticExporter(logger, appInfo.ApplicationDataPath);

// 診断情報をエクスポート
var outputPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    $"diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
);

var result = await exporter.ExportDiagnosticsAsync(outputPath);
if (result.IsSuccess)
{
    Console.WriteLine($"Diagnostics exported to: {result.Value}");
}
```

### エクスポートオプション

```csharp
var options = new DiagnosticExportOptions
{
    IncludeSystemInfo = true,
    IncludeApplicationInfo = true,
    IncludeLogs = true,
    LogFilesPattern = "*.log", // すべてのログファイル
    IncludeConfiguration = true,
    IncludeCrashReports = true,
    IncludeState = true,
    CustomFilePaths = new[]
    {
        "data/database.db",
        "data/settings.json"
    }
};

var result = await exporter.ExportDiagnosticsAsync(outputPath, options);
```

### エクスポート内容

生成されるZIPファイルの構造：

```
diagnostics_20240115_103045.zip
├── system_info.json           # システム情報
├── application_info.json      # アプリケーション情報
├── logs/                      # ログファイル
│   ├── app_20240115.log
│   └── app_20240114.log
├── config/                    # 設定ファイル
│   └── appsettings.json
├── crash_reports/             # クラッシュレポート（最新10件）
│   ├── crash_20240115_100000.json
│   └── crash_20240114_150000.json
├── state.json                 # 状態ファイル
└── custom/                    # カスタムファイル
    ├── database.db
    └── settings.json
```

## ViewModelでの使用

### 診断情報エクスポート機能

```csharp
public class SettingsViewModel : ViewModelBase
{
    private readonly DiagnosticExporter _exporter;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SettingsViewModel> _logger;

    public AsyncDelegateCommand ExportDiagnosticsCommand { get; }

    public SettingsViewModel(
        DiagnosticExporter exporter,
        IDialogService dialogService,
        ILogger<SettingsViewModel> logger)
    {
        _exporter = exporter;
        _dialogService = dialogService;
        _logger = logger;

        ExportDiagnosticsCommand = new AsyncDelegateCommand(ExportDiagnosticsAsync);
    }

    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            IsBusy = true;

            var outputPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            );

            var result = await _exporter.ExportDiagnosticsAsync(outputPath);
            if (result.IsSuccess)
            {
                await _dialogService.ShowInformationAsync(
                    "成功",
                    $"診断情報をエクスポートしました:\n{result.Value}"
                );
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    "エラー",
                    $"エクスポートに失敗しました: {result.ErrorMessage}"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export diagnostics");
            await _dialogService.ShowErrorAsync("エラー", "エクスポートに失敗しました");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### クラッシュレポート一覧

```csharp
public class DiagnosticsViewModel : ViewModelBase
{
    private readonly GlobalExceptionHandler _exceptionHandler;
    private ObservableCollection<CrashReportInfo> _crashReports = new();

    public ObservableCollection<CrashReportInfo> CrashReports
    {
        get => _crashReports;
        set => this.RaiseAndSetIfChanged(ref _crashReports, value);
    }

    public AsyncDelegateCommand LoadCrashReportsCommand { get; }
    public AsyncDelegateCommand<CrashReportInfo> ViewReportCommand { get; }

    public DiagnosticsViewModel(GlobalExceptionHandler exceptionHandler)
    {
        _exceptionHandler = exceptionHandler;

        LoadCrashReportsCommand = new AsyncDelegateCommand(LoadCrashReportsAsync);
        ViewReportCommand = new AsyncDelegateCommand<CrashReportInfo>(ViewReportAsync);
    }

    public override void OnInitialize()
    {
        LoadCrashReportsCommand.ExecuteAsync();
    }

    private async Task LoadCrashReportsAsync()
    {
        try
        {
            IsBusy = true;

            var reportsResult = _exceptionHandler.GetCrashReports();
            if (reportsResult.IsSuccess)
            {
                var reports = new List<CrashReportInfo>();

                foreach (var reportPath in reportsResult.Value!)
                {
                    var reportResult = await _exceptionHandler.LoadCrashReportAsync(reportPath);
                    if (reportResult.IsSuccess)
                    {
                        reports.Add(new CrashReportInfo
                        {
                            FilePath = reportPath,
                            Report = reportResult.Value!
                        });
                    }
                }

                CrashReports = new ObservableCollection<CrashReportInfo>(reports);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ViewReportAsync(CrashReportInfo? reportInfo)
    {
        if (reportInfo == null) return;

        // レポート詳細を表示
        // ...
    }
}

public class CrashReportInfo
{
    public string FilePath { get; set; } = string.Empty;
    public CrashReport Report { get; set; } = new();
}
```

## DI統合

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    var appInfo = new ApplicationInfo();

    // GlobalExceptionHandler
    containerRegistry.RegisterSingleton<GlobalExceptionHandler>(provider =>
    {
        var logger = provider.Resolve<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(logger, appInfo.ApplicationDataPath);

        // ファイルハンドラーを登録
        var fileLogger = provider.Resolve<ILogger<FileExceptionHandler>>();
        var logPath = Path.Combine(appInfo.ApplicationDataPath, "exceptions.log");
        handler.RegisterHandler(new FileExceptionHandler(fileLogger, logPath));

        // 通知ハンドラーを登録
        var notificationService = provider.Resolve<INotificationService>();
        handler.RegisterHandler(new NotificationExceptionHandler(notificationService));

        return handler;
    });

    // DiagnosticExporter
    containerRegistry.RegisterSingleton<DiagnosticExporter>(provider =>
    {
        var logger = provider.Resolve<ILogger<DiagnosticExporter>>();
        return new DiagnosticExporter(logger, appInfo.ApplicationDataPath);
    });
}
```

## ベストプラクティス

1. **早期セットアップ**: アプリ起動直後に例外ハンドラーを設定
2. **すべてのイベント**: AppDomain, Dispatcher, Task のすべてをフック
3. **ユーザー通知**: エラー発生時はユーザーに通知
4. **自動エクスポート**: サポート依頼時に診断情報を簡単にエクスポート
5. **プライバシー**: 個人情報を含むファイルはエクスポートから除外

```csharp
// 良い例: 包括的な例外処理
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var exceptionHandler = Container.Resolve<GlobalExceptionHandler>();

    AppDomain.CurrentDomain.UnhandledException += (s, e) =>
    {
        if (e.ExceptionObject is Exception ex)
        {
            exceptionHandler.HandleExceptionAsync(ex, "AppDomain").Wait();
        }
    };

    DispatcherUnhandledException += (s, e) =>
    {
        exceptionHandler.HandleExceptionAsync(e.Exception, "Dispatcher").Wait();
        e.Handled = true;
    };

    TaskScheduler.UnobservedTaskException += (s, e) =>
    {
        exceptionHandler.HandleExceptionAsync(e.Exception, "Task").Wait();
        e.SetObserved();
    };
}

// 悪い例: 例外を無視
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    DispatcherUnhandledException += (s, e) =>
    {
        e.Handled = true;  // 何も記録せずに無視 NG!
    };
}
```

## 他モジュールとの統合

- **Application**: クラッシュリカバリーとの連携
- **Logging**: Serilog との統合
- **Notifications**: エラー通知
- **FileManagement**: 診断ファイルのエクスポート

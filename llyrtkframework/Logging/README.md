# Logging モジュール

Serilogベースの高機能ロギングシステム

## 機能

- ✅ ファイル・コンソールへの出力
- ✅ ローリングファイル（日次/サイズベース）
- ✅ 構造化ロギング
- ✅ カスタムEnricher（UserID、SessionID等）
- ✅ パフォーマンス計測
- ✅ DI統合（Prism/DryIoc対応）

## クイックスタート

### 1. 基本的な使用方法

```csharp
// App.xaml.cs
using llyrtkframework.Logging;
using Serilog;

public class App : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 方法1: デフォルト設定で登録
        containerRegistry.AddLlyrtkLogging();

        // 方法2: アプリケーション名を指定
        containerRegistry.AddLlyrtkLogging("MyAvaloniaApp");

        // 方法3: 本番環境モード
        containerRegistry.AddLlyrtkLogging("MyAvaloniaApp", isProduction: true);

        // 方法4: カスタム設定
        containerRegistry.AddLlyrtkLogging(config =>
        {
            config.CreateDefaultConfiguration("MyApp")
                  .MinimumLevel.Information()
                  .AddErrorFileLogging("MyApp")  // エラーログを別ファイルに
                  .AddJsonFileLogging("MyApp");  // JSON形式でも出力
        });
    }
}
```

### 2. ViewModel での使用

```csharp
using Microsoft.Extensions.Logging;

public class MainViewModel : BindableBase
{
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(ILogger<MainViewModel> logger)
    {
        _logger = logger;
        _logger.LogInformation("MainViewModel initialized");
    }

    public async Task LoadDataAsync()
    {
        _logger.LogDebug("Loading data for user {UserId}", userId);

        try
        {
            var data = await _service.GetDataAsync();
            _logger.LogInformation("Data loaded successfully. Count: {Count}", data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to load data");
        }
    }
}
```

### 3. パフォーマンス計測

```csharp
public async Task ProcessLargeDataAsync()
{
    using (_logger.BeginTimedOperation("ProcessLargeData"))
    {
        // 処理...
        await Task.Delay(1000);
    }
    // 自動的に "ProcessLargeData completed in 1000ms" とログ出力
}
```

### 4. コンテキスト情報の追加

```csharp
using llyrtkframework.Logging;

public async Task ExecuteOperationAsync(string userId)
{
    // ユーザーIDをログに自動付与
    using (LoggingHelper.PushProperty("UserId", userId))
    {
        _logger.LogInformation("Starting operation");
        // すべてのログに UserId が含まれる

        await DoSomethingAsync();
        _logger.LogInformation("Operation completed");
    }
}

// 複数プロパティを一度に追加
using (LoggingHelper.PushProperties(
    ("UserId", userId),
    ("SessionId", sessionId),
    ("RequestId", requestId)))
{
    _logger.LogInformation("Processing request");
}
```

### 5. 構造化ログ

```csharp
var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

// オブジェクト全体をログに記録
_logger.LogObject("User created", user);

// 出力例:
// User created: {"Id":1,"Name":"John","Email":"john@example.com"}
```

### 6. 例外の詳細ログ

```csharp
try
{
    await RiskyOperationAsync();
}
catch (Exception ex)
{
    // InnerException も含めて詳細にログ出力
    _logger.LogException(ex, "Operation failed for user {UserId}", userId);
}
```

## 設定テンプレート

### 開発環境向け

```csharp
Log.Logger = LoggerConfigurationExtensions
    .CreateDevelopmentConfiguration("MyApp")
    .CreateLogger();
```

**特徴:**
- すべてのログレベルを出力（Verbose）
- コンソールとファイルの両方に出力
- 7日分のログを保持

### 本番環境向け

```csharp
Log.Logger = LoggerConfigurationExtensions
    .CreateProductionConfiguration("MyApp")
    .CreateLogger();
```

**特徴:**
- Information 以上のみ出力
- ファイルのみに出力
- 90日分のログを保持
- ファイルサイズ100MBで分割

### カスタムEnricher使用

```csharp
Log.Logger = new LoggerConfiguration()
    .CreateDefaultConfiguration("MyApp")
    .WithUserId(currentUserId)
    .WithSessionId(sessionId)
    .WithApplicationVersion("1.0.0")
    .CreateLogger();
```

## 出力先

### ログファイル

デフォルトでは以下の場所に出力されます:

```
<実行フォルダ>/logs/
  ├── MyApp-20241219.log       # 通常ログ（日次ローテーション）
  ├── MyApp-errors-20241219.log # エラーログのみ
  └── MyApp-20241219.json       # JSON形式
```

### ログフォーマット

**通常ログ:**
```
2024-12-19 12:34:56.789 +09:00 [INF] [MyApp.ViewModels.MainViewModel] User logged in UserId=123
```

**コンソール出力:**
```
[12:34:56 INF] MyApp.ViewModels.MainViewModel User logged in {"UserId":123}
```

## ログレベル

| レベル | 用途 |
|--------|------|
| **Verbose** | 詳細なトレース情報 |
| **Debug** | デバッグ情報 |
| **Information** | 一般的な情報 |
| **Warning** | 警告（処理は継続） |
| **Error** | エラー（処理失敗） |
| **Fatal** | 致命的なエラー（アプリ停止） |

## ベストプラクティス

### 1. 構造化ロギングを活用

❌ **悪い例:**
```csharp
_logger.LogInformation($"User {userId} logged in at {DateTime.Now}");
```

✅ **良い例:**
```csharp
_logger.LogInformation("User {UserId} logged in at {Timestamp}", userId, DateTime.Now);
```

### 2. ログレベルを適切に選択

```csharp
_logger.LogDebug("Method entered");           // 開発中のみ
_logger.LogInformation("User action");        // 通常の操作
_logger.LogWarning("Retry attempt {Count}", retryCount);  // 注意が必要
_logger.LogError(ex, "Operation failed");     // エラー
```

### 3. センシティブ情報をログに含めない

❌ **危険:**
```csharp
_logger.LogInformation("Password: {Password}", password);
```

✅ **安全:**
```csharp
_logger.LogInformation("User authenticated successfully");
```

### 4. パフォーマンス計測を活用

```csharp
// 重い処理の前後で計測
using (_logger.BeginTimedOperation("DatabaseQuery"))
{
    await _repository.GetAllAsync();
}
```

## トラブルシューティング

### ログファイルが作成されない

**原因:** 書き込み権限がない

**解決策:**
```csharp
// 絶対パスを指定
.WriteTo.File(@"C:\MyApp\Logs\app.log")
```

### ログが出力されない

**原因:** ログレベルが高すぎる

**解決策:**
```csharp
.MinimumLevel.Verbose()  // すべて出力
```

### パフォーマンス問題

**原因:** 大量のログ出力

**解決策:**
```csharp
// 本番環境では Information 以上のみ
.MinimumLevel.Information()
// または条件付きログ
if (_logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug("Heavy operation result: {@Result}", result);
}
```

## 関連ドキュメント

- [Serilog 公式ドキュメント](https://serilog.net/)
- [Microsoft.Extensions.Logging](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/logging)

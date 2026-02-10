# Localization モジュール

多言語対応（i18n）を提供するモジュールです。

## 概要

Localizationモジュールは、アプリケーションの国際化を提供します：

- **多言語リソース**: 複数言語のリソース管理
- **動的切り替え**: 実行時に言語切り替え可能
- **フォールバック**: 親カルチャへの自動フォールバック
- **リアクティブ**: 言語変更を自動通知
- **JSONサポート**: JSONファイルからリソース読み込み

## 主要コンポーネント

### ILocalizationService

ローカライゼーションサービスのインターフェース。

```csharp
public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    Result SetCulture(CultureInfo culture);
    Result SetCulture(string cultureName);
    Result<string> GetString(string key);
    Result<string> GetString(string key, params object[] args);
    bool ContainsKey(string key);
    IEnumerable<CultureInfo> GetAvailableCultures();
    IObservable<CultureInfo> CultureChanged { get; }
}
```

### LocalizationService

ローカライゼーションサービスの実装クラス。

```csharp
var service = new LocalizationService(logger);

// リソース追加
service.AddResource("en-US", "Greeting", "Hello");
service.AddResource("ja-JP", "Greeting", "こんにちは");

// カルチャ設定
service.SetCulture("ja-JP");

// 文字列取得
var greeting = service.GetString("Greeting");
Console.WriteLine(greeting.Value);  // "こんにちは"
```

## 使用例

### 基本的な使用方法

```csharp
var service = new LocalizationService(logger);

// リソースを追加
service.AddResource("en-US", "AppName", "My Application");
service.AddResource("en-US", "SaveButton", "Save");
service.AddResource("en-US", "CancelButton", "Cancel");

service.AddResource("ja-JP", "AppName", "マイアプリケーション");
service.AddResource("ja-JP", "SaveButton", "保存");
service.AddResource("ja-JP", "CancelButton", "キャンセル");

// カルチャ設定
service.SetCulture("ja-JP");

// 文字列取得
var appName = service.GetString("AppName");
Console.WriteLine(appName.Value);  // "マイアプリケーション"

// カルチャ変更
service.SetCulture("en-US");
var appNameEn = service.GetString("AppName");
Console.WriteLine(appNameEn.Value);  // "My Application"
```

### フォーマット付き文字列

```csharp
// リソース定義
service.AddResource("en-US", "Welcome", "Welcome, {0}!");
service.AddResource("ja-JP", "Welcome", "ようこそ、{0}さん！");

service.AddResource("en-US", "ItemCount", "You have {0} item(s).");
service.AddResource("ja-JP", "ItemCount", "{0}個のアイテムがあります。");

// 使用
service.SetCulture("ja-JP");

var welcome = service.GetString("Welcome", "太郎");
Console.WriteLine(welcome.Value);  // "ようこそ、太郎さん！"

var itemCount = service.GetString("ItemCount", 5);
Console.WriteLine(itemCount.Value);  // "5個のアイテムがあります。"
```

### JSONファイルからの読み込み

```json
// resources/ja-JP.json
{
  "AppName": "マイアプリケーション",
  "SaveButton": "保存",
  "CancelButton": "キャンセル",
  "Welcome": "ようこそ、{0}さん！",
  "Error": {
    "NotFound": "見つかりませんでした",
    "NetworkError": "ネットワークエラーが発生しました"
  }
}
```

```csharp
var service = new LocalizationService(logger);

// JSONから読み込み
await service.LoadResourcesFromFileAsync("en-US", "resources/en-US.json");
await service.LoadResourcesFromFileAsync("ja-JP", "resources/ja-JP.json");

service.SetCulture("ja-JP");

var appName = service.GetString("AppName");
Console.WriteLine(appName.Value);  // "マイアプリケーション"
```

### ViewModelでの使用

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private string _greeting = string.Empty;
    private IDisposable? _cultureSubscription;

    public string Greeting
    {
        get => _greeting;
        set => this.RaiseAndSetIfChanged(ref _greeting, value);
    }

    public DelegateCommand<string> ChangeCultureCommand { get; }

    public MainViewModel(ILocalizationService localization)
    {
        _localization = localization;

        ChangeCultureCommand = new DelegateCommand<string>(cultureName =>
        {
            if (!string.IsNullOrEmpty(cultureName))
            {
                _localization.SetCulture(cultureName);
            }
        });

        // カルチャ変更を監視
        _cultureSubscription = _localization.CultureChanged
            .Subscribe(culture =>
            {
                UpdateLocalizedStrings();
            });

        UpdateLocalizedStrings();
    }

    private void UpdateLocalizedStrings()
    {
        Greeting = _localization.GetString("Greeting").Value ?? "Hello";
    }

    public override void OnDeactivated()
    {
        _cultureSubscription?.Dispose();
    }
}
```

### リアクティブなローカライゼーション

```csharp
public class LanguageSwitcherViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private string _currentLanguage = string.Empty;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _currentLanguage, value))
            {
                _localization.SetCulture(value);
            }
        }
    }

    public List<LanguageOption> AvailableLanguages { get; }

    public LanguageSwitcherViewModel(ILocalizationService localization)
    {
        _localization = localization;

        AvailableLanguages = new List<LanguageOption>
        {
            new() { Code = "en-US", Name = "English" },
            new() { Code = "ja-JP", Name = "日本語" },
            new() { Code = "zh-CN", Name = "中文" }
        };

        CurrentLanguage = _localization.CurrentCulture.Name;
    }
}

public class LanguageOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
```

### 拡張メソッドの使用

```csharp
// 文字列にローカライズを適用
var greeting = "Greeting".Localize(service);
Console.WriteLine(greeting);  // 現在のカルチャでローカライズされた文字列

// フォーマット付き
var welcome = "Welcome".Localize(service, "太郎");
Console.WriteLine(welcome);  // "ようこそ、太郎さん！"

// 複数のキーを一括ローカライズ
var keys = new[] { "AppName", "SaveButton", "CancelButton" };
var localized = keys.LocalizeMany(service);

foreach (var (key, value) in localized)
{
    Console.WriteLine($"{key}: {value}");
}
```

## DI統合

```csharp
// App.xaml.cs
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // Singleton登録
    containerRegistry.RegisterSingleton<ILocalizationService>(provider =>
    {
        var logger = provider.Resolve<ILogger<LocalizationService>>();
        var service = new LocalizationService(logger, CultureInfo.CurrentCulture);

        // リソース初期化
        InitializeResourcesAsync(service).Wait();

        return service;
    });
}

private async Task InitializeResourcesAsync(LocalizationService service)
{
    // JSONファイルから読み込み
    await service.LoadResourcesFromFileAsync("en-US", "Resources/en-US.json");
    await service.LoadResourcesFromFileAsync("ja-JP", "Resources/ja-JP.json");
    await service.LoadResourcesFromFileAsync("zh-CN", "Resources/zh-CN.json");

    // またはコードで定義
    var enResources = new Dictionary<string, string>
    {
        ["AppName"] = "My Application",
        ["SaveButton"] = "Save",
        ["CancelButton"] = "Cancel"
    };
    service.AddResources("en-US", enResources);
}
```

## グローバルローカライゼーション

アプリ全体で使用する定数化されたキー。

```csharp
public static class LocalizationKeys
{
    public const string AppName = "AppName";
    public const string SaveButton = "SaveButton";
    public const string CancelButton = "CancelButton";
    public const string DeleteButton = "DeleteButton";

    public static class Errors
    {
        public const string NotFound = "Error.NotFound";
        public const string NetworkError = "Error.NetworkError";
        public const string ValidationError = "Error.ValidationError";
    }

    public static class Messages
    {
        public const string SaveSuccess = "Message.SaveSuccess";
        public const string DeleteConfirm = "Message.DeleteConfirm";
    }
}

// 使用
var saveButton = service.GetString(LocalizationKeys.SaveButton);
var errorMsg = service.GetString(LocalizationKeys.Errors.NotFound);
```

## カスタムマークアップ拡張（Avalonia）

```csharp
// LocalizeExtension.cs
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localization = App.Current.Container.Resolve<ILocalizationService>();
        return localization.GetString(Key).Value ?? $"[{Key}]";
    }
}

// XAML
<TextBlock Text="{local:Localize Key=AppName}" />
<Button Content="{local:Localize Key=SaveButton}" />
```

## リソースファイル構造例

```
Resources/
├── en-US.json
├── ja-JP.json
├── zh-CN.json
└── fr-FR.json
```

```json
// en-US.json
{
  "Common": {
    "AppName": "My Application",
    "Version": "Version {0}"
  },
  "Buttons": {
    "Save": "Save",
    "Cancel": "Cancel",
    "Delete": "Delete",
    "Edit": "Edit"
  },
  "Messages": {
    "SaveSuccess": "Saved successfully",
    "DeleteConfirm": "Are you sure you want to delete this item?",
    "NetworkError": "Network error occurred"
  },
  "Validation": {
    "Required": "{0} is required",
    "EmailInvalid": "Invalid email address",
    "PasswordTooShort": "Password must be at least {0} characters"
  }
}
```

## 階層的なキー管理

```csharp
// リソース登録時にドット記法を使用
service.AddResource("en-US", "Common.AppName", "My Application");
service.AddResource("en-US", "Buttons.Save", "Save");
service.AddResource("en-US", "Messages.SaveSuccess", "Saved successfully");

// 取得
var appName = service.GetString("Common.AppName");
var saveButton = service.GetString("Buttons.Save");
```

## カルチャフォールバック

```csharp
// zh-CNにリソースがない場合、zhにフォールバック
service.AddResource("zh", "AppName", "我的应用程序");

service.SetCulture("zh-CN");

// zh-CNにAppNameがなければ、zhのAppNameを返す
var appName = service.GetString("AppName");
```

## StateManagementとの統合

```csharp
public class LocalizationStateService
{
    private readonly ILocalizationService _localization;
    private readonly IStateStore _stateStore;

    public LocalizationStateService(
        ILocalizationService localization,
        IStateStore stateStore)
    {
        _localization = localization;
        _stateStore = stateStore;

        // 現在のカルチャを状態として保存
        _localization.CultureChanged.Subscribe(culture =>
        {
            _stateStore.SetState("CurrentCulture", culture.Name);
        });

        // 起動時に前回のカルチャを復元
        var savedCulture = _stateStore.GetState<string>("CurrentCulture");
        if (savedCulture.IsSuccess)
        {
            _localization.SetCulture(savedCulture.Value!);
        }
    }
}
```

## ベストプラクティス

1. **キーの定数化**: マジックストリングを避ける
2. **階層構造**: ドット記法で論理的にグループ化
3. **JSONファイル**: リソースはJSONで外部管理
4. **フォールバック**: 親カルチャのリソースを用意
5. **リアクティブ**: カルチャ変更を購読してUI更新

```csharp
// 良い例
public static class L10n
{
    public const string SaveButton = "Buttons.Save";
    public const string CancelButton = "Buttons.Cancel";
}

var text = service.GetString(L10n.SaveButton);

// 悪い例
var text = service.GetString("save_btn");  // マジックストリング
```

## 他モジュールとの統合

- **MVVM**: ViewModelでの多言語対応
- **StateManagement**: カルチャの永続化
- **Configuration**: 言語設定の保存
- **Events**: カルチャ変更イベント

# Configuration モジュール

アプリケーション設定の読み書き、永続化を提供するモジュールです。

## 概要

Configurationモジュールは、型安全な設定管理を提供します：

- **型付き設定**: 任意の型で設定を管理
- **永続化**: FileManagementモジュールと統合
- **デフォルト値**: 設定のリセット機能
- **Result パターン**: 一貫したエラーハンドリング

## 主要コンポーネント

### IConfigurationManager

設定管理のインターフェース。

```csharp
public interface IConfigurationManager
{
    Result<T> GetValue<T>(string key, T defaultValue = default!);
    Result SetValue<T>(string key, T value);
    bool ContainsKey(string key);
    Result RemoveValue(string key);
    Result Clear();
    Task<Result> SaveAsync();
    Task<Result> LoadAsync();
    Result Reset();
}
```

### ConfigurationManager

設定管理の実装クラス。

```csharp
// ファイル永続化なし
var config = new ConfigurationManager(logger);

// ファイル永続化あり
var config = new ConfigurationManager(logger, "config.json");
```

## 使用例

### 基本的な使用方法

```csharp
var config = new ConfigurationManager(logger, "appsettings.json");

// 設定値の取得
var themeResult = config.GetValue<string>("Theme", "Light");
if (themeResult.IsSuccess)
{
    Console.WriteLine($"Theme: {themeResult.Value}");
}

// 設定値の設定
config.SetValue("Theme", "Dark");
config.SetValue("FontSize", 14);
config.SetValue("Language", "ja-JP");

// ファイルに保存
await config.SaveAsync();

// ファイルから読み込み
await config.LoadAsync();
```

### 複雑な型の管理

```csharp
// カスタムクラス
public class WindowSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsMaximized { get; set; }
}

// 保存
var windowSettings = new WindowSettings
{
    Width = 1024,
    Height = 768,
    IsMaximized = false
};
config.SetValue("Window", windowSettings);
await config.SaveAsync();

// 読み込み
var result = config.GetValue<WindowSettings>("Window");
if (result.IsSuccess)
{
    var settings = result.Value!;
    Console.WriteLine($"Size: {settings.Width}x{settings.Height}");
}
```

### デフォルト値の設定

```csharp
var config = new ConfigurationManager(logger, "config.json");

// デフォルト値を設定
config.SetDefaultValue("Theme", "Light");
config.SetDefaultValue("Language", "en-US");
config.SetDefaultValue("FontSize", 12);

// 設定をリセット（デフォルト値に戻る）
config.Reset();
```

### 設定の存在確認と削除

```csharp
// 存在確認
if (config.ContainsKey("Theme"))
{
    Console.WriteLine("Theme設定が存在します");
}

// 削除
config.RemoveValue("Theme");

// すべてクリア
config.Clear();
```

## 拡張メソッド

### GetOrSetValue

設定が存在しない場合は自動的に設定します。

```csharp
// 存在しなければ設定してから返す
var themeResult = config.GetOrSetValue("Theme", "Light");
Console.WriteLine($"Theme: {themeResult.Value}");
```

### GetValues

複数の設定値を一括取得。

```csharp
var result = config.GetValues<string>("Theme", "Language", "FontFamily");
if (result.IsSuccess)
{
    foreach (var (key, value) in result.Value!)
    {
        Console.WriteLine($"{key}: {value}");
    }
}
```

### SetValues

複数の設定値を一括設定。

```csharp
var settings = new Dictionary<string, string>
{
    ["Theme"] = "Dark",
    ["Language"] = "ja-JP",
    ["FontFamily"] = "Meiryo UI"
};

config.SetValues(settings);
await config.SaveAsync();
```

## ViewModelとの統合

```csharp
public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationManager _config;
    private string _theme = "Light";
    private int _fontSize = 12;

    public string Theme
    {
        get => _theme;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _theme, value))
            {
                _config.SetValue("Theme", value);
            }
        }
    }

    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _fontSize, value))
            {
                _config.SetValue("FontSize", value);
            }
        }
    }

    public AsyncDelegateCommand SaveCommand { get; }
    public DelegateCommand ResetCommand { get; }

    public SettingsViewModel(IConfigurationManager config)
    {
        _config = config;

        SaveCommand = new AsyncDelegateCommand(SaveAsync);
        ResetCommand = new DelegateCommand(Reset);

        LoadSettings();
    }

    private void LoadSettings()
    {
        _theme = _config.GetValue("Theme", "Light").Value!;
        _fontSize = _config.GetValue("FontSize", 12).Value;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await _config.SaveAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Reset()
    {
        _config.Reset();
        LoadSettings();
    }
}
```

## DI統合

```csharp
// App.xaml.cs
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // Singleton登録
    containerRegistry.RegisterSingleton<IConfigurationManager>(provider =>
    {
        var logger = provider.Resolve<ILogger<ConfigurationManager>>();
        var config = new ConfigurationManager(logger, "appsettings.json");

        // デフォルト値設定
        config.SetDefaultValue("Theme", "Light");
        config.SetDefaultValue("Language", "en-US");
        config.SetDefaultValue("FontSize", 12);

        // 初回読み込み
        config.LoadAsync().Wait();

        return config;
    });
}
```

## アプリケーション設定クラス

より構造化された設定管理のために、専用クラスを作成することも可能です。

```csharp
public class AppSettings
{
    private readonly IConfigurationManager _config;

    public AppSettings(IConfigurationManager config)
    {
        _config = config;
    }

    public string Theme
    {
        get => _config.GetValue("Theme", "Light").Value!;
        set => _config.SetValue("Theme", value);
    }

    public string Language
    {
        get => _config.GetValue("Language", "en-US").Value!;
        set => _config.SetValue("Language", value);
    }

    public int FontSize
    {
        get => _config.GetValue("FontSize", 12).Value;
        set => _config.SetValue("FontSize", value);
    }

    public WindowSettings Window
    {
        get => _config.GetValue("Window", new WindowSettings()).Value!;
        set => _config.SetValue("Window", value);
    }

    public async Task SaveAsync() => await _config.SaveAsync();
    public async Task LoadAsync() => await _config.LoadAsync();
    public void Reset() => _config.Reset();
}

// 使用
var settings = new AppSettings(config);
settings.Theme = "Dark";
settings.FontSize = 14;
await settings.SaveAsync();
```

## FileManagement統合

ConfigurationManagerは内部でFileManagementモジュールを使用しています。

```csharp
// 自動バックアップ付きの設定ファイル
// ConfigurationManagerは内部で以下のようにFileManagerを使用
private class ConfigurationFileManager : FileManagerBase<Dictionary<string, JsonElement>>
{
    public ConfigurationFileManager(
        FileManagement.Core.FileOptions options,
        IFileSerializer<Dictionary<string, JsonElement>> serializer,
        ILogger logger)
        : base(options, serializer, logger)
    {
    }
}
```

設定ファイルは自動的にバックアップされ、FileManagerRegistryで管理されます。

## ベストプラクティス

1. **Singleton管理**: アプリ全体で1インスタンス
2. **デフォルト値の設定**: アプリ起動時に設定
3. **定期的な保存**: 重要な設定変更後は即座に保存
4. **型安全性**: 設定のキーは定数化
5. **エラーハンドリング**: Resultパターンで常にチェック

```csharp
public static class ConfigKeys
{
    public const string Theme = "Theme";
    public const string Language = "Language";
    public const string FontSize = "FontSize";
}

// 使用
config.SetValue(ConfigKeys.Theme, "Dark");
```

## 設定ファイル例

```json
{
  "Theme": "Dark",
  "Language": "ja-JP",
  "FontSize": 14,
  "Window": {
    "Width": 1024,
    "Height": 768,
    "IsMaximized": false
  }
}
```

## 他モジュールとの統合

- **FileManagement**: 設定の永続化とバックアップ
- **Results**: エラーハンドリング
- **Logging**: 設定変更のログ記録
- **MVVM**: ViewModelでの設定管理

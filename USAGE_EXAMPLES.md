# llyrtkframework 使用例ドキュメント

## 📚 目次

1. [StateStore と Configuration の統合](#1-statestore-と-configuration-の統合)
2. [ViewModel での自動バリデーション](#2-viewmodel-での自動バリデーション)
3. [FileManagement の自動保存機能](#3-filemanagement-の自動保存機能)

---

## 1. StateStore と Configuration の統合

### PersistentStateManager の使用

`PersistentStateManager` を使用すると、一時的な状態（StateStore）と永続化される設定（ConfigurationManager）を統合して管理できます。

```csharp
using llyrtkframework.StateManagement;
using llyrtkframework.Configuration;
using Microsoft.Extensions.Logging;

// セットアップ
var stateStore = new StateStore(logger);
var configManager = new ConfigurationManager(logger, "appsettings.json");
var persistentStateManager = new PersistentStateManager(
    stateStore,
    configManager,
    logger);

// アプリ起動時に永続化された状態をロード
await persistentStateManager.LoadPersistentStatesAsync();

// 永続化される状態を設定（ファイルに自動保存される）
persistentStateManager.SetPersistentState("Theme", "Dark");
persistentStateManager.SetPersistentState("Language", "ja-JP");
persistentStateManager.SetPersistentState("WindowSize", new WindowSize { Width = 1920, Height = 1080 });

// 一時的な状態を設定（メモリのみ、再起動で消える）
persistentStateManager.SetTransientState("CurrentUser", userObject);
persistentStateManager.SetTransientState("SessionId", sessionId);

// 状態を取得
var themeResult = persistentStateManager.GetState<string>("Theme");
if (themeResult.IsSuccess)
{
    Console.WriteLine($"Current theme: {themeResult.Value}");
}

// 状態変更の監視（リアクティブ）
persistentStateManager.StateChanged
    .Where(e => e.Key == "Theme")
    .Subscribe(e =>
    {
        Console.WriteLine($"Theme changed: {e.OldValue} → {e.NewValue}");
        ApplyTheme((string)e.NewValue!);
    });

// 自動保存の無効化（一時的にバッチ更新する場合）
persistentStateManager.SetAutoSaveEnabled(false);

// 複数の設定を一括変更
persistentStateManager.SetPersistentState("Setting1", value1);
persistentStateManager.SetPersistentState("Setting2", value2);
persistentStateManager.SetPersistentState("Setting3", value3);

// 手動保存
await persistentStateManager.SaveAllPersistentStatesAsync();

// 自動保存を再開
persistentStateManager.SetAutoSaveEnabled(true);
```

---

## 2. ViewModel での自動バリデーション

### ValidatableViewModelBase の使用

`ValidatableViewModelBase` を使用すると、プロパティ変更時に自動的にバリデーションが実行され、UIにエラーが表示されます。

#### Step 1: Validator を定義

```csharp
using FluentValidation;
using llyrtkframework.Validation;

public class UserValidator : AbstractValidatorBase<UserViewModel>
{
    public UserValidator()
    {
        // 基本的なバリデーション
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("メールアドレスは必須です")
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください");

        RuleFor(x => x.Age)
            .InclusiveBetween(0, 150).WithMessage("年齢は0〜150の範囲で入力してください");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("ユーザー名は必須です")
            .Length(3, 20).WithMessage("ユーザー名は3〜20文字で入力してください");

        // クロスプロパティバリデーション
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .When(x => !string.IsNullOrEmpty(x.Password))
            .WithMessage("パスワードが一致しません");

        // カスタムバリデーション（AbstractValidatorBase のヘルパーメソッド）
        RuleFor(x => x.WebsiteUrl)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("有効なURLを入力してください");
    }
}
```

#### Step 2: ViewModel を実装

```csharp
using llyrtkframework.Mvvm;
using ReactiveUI;

public class UserViewModel : ValidatableViewModelBase<UserViewModel, UserValidator>
{
    private string _email = "";
    private int _age;
    private string _userName = "";
    private string _password = "";
    private string _confirmPassword = "";
    private string _websiteUrl = "";

    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
        // => 300ms後に自動的にバリデーションが実行される
    }

    public int Age
    {
        get => _age;
        set => this.RaiseAndSetIfChanged(ref _age, value);
    }

    public string UserName
    {
        get => _userName;
        set => this.RaiseAndSetIfChanged(ref _userName, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
    }

    public string WebsiteUrl
    {
        get => _websiteUrl;
        set => this.RaiseAndSetIfChanged(ref _websiteUrl, value);
    }

    // 保存コマンド
    public AsyncDelegateCommand SaveCommand { get; }

    public UserViewModel()
    {
        // デバウンス時間をカスタマイズ（デフォルト: 300ms）
        ValidationDebounce = TimeSpan.FromMilliseconds(500);

        SaveCommand = new AsyncDelegateCommand(
            execute: async () => await SaveAsync(),
            canExecute: () => !HasErrors && !IsBusy);

        // HasErrors が変更されたら SaveCommand の実行可否を再評価
        this.WhenAnyValue(x => x.HasErrors, x => x.IsBusy)
            .Subscribe(_ => SaveCommand.RaiseCanExecuteChanged());
    }

    private async Task SaveAsync()
    {
        // 保存前に明示的なバリデーション
        if (!await ValidateAsync())
        {
            // エラーは既にUIに表示されている
            return;
        }

        IsBusy = true;

        try
        {
            // 保存処理
            await userService.SaveUserAsync(new User
            {
                Email = Email,
                Age = Age,
                UserName = UserName,
                Password = Password,
                WebsiteUrl = WebsiteUrl
            });

            // 成功通知
            notificationService.ShowSuccess("ユーザー情報を保存しました");
        }
        catch (Exception ex)
        {
            notificationService.ShowError($"保存に失敗しました: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // バリデーションエラーの取得
    public void ShowAllErrors()
    {
        var errors = GetAllErrorMessages();
        foreach (var error in errors)
        {
            Console.WriteLine(error);
        }
    }
}
```

#### Step 3: View (Avalonia XAML)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Spacing="10" Margin="20">
        <!-- メールアドレス -->
        <TextBox Text="{Binding Email, Mode=TwoWay}"
                 Watermark="メールアドレス"
                 IsEnabled="{Binding !IsBusy}">
            <!-- バリデーションエラーが自動表示される -->
        </TextBox>

        <!-- 年齢 -->
        <NumericUpDown Value="{Binding Age, Mode=TwoWay}"
                       Watermark="年齢"
                       IsEnabled="{Binding !IsBusy}" />

        <!-- ユーザー名 -->
        <TextBox Text="{Binding UserName, Mode=TwoWay}"
                 Watermark="ユーザー名"
                 IsEnabled="{Binding !IsBusy}" />

        <!-- パスワード -->
        <TextBox Text="{Binding Password, Mode=TwoWay}"
                 Watermark="パスワード"
                 PasswordChar="●"
                 IsEnabled="{Binding !IsBusy}" />

        <!-- パスワード確認 -->
        <TextBox Text="{Binding ConfirmPassword, Mode=TwoWay}"
                 Watermark="パスワード確認"
                 PasswordChar="●"
                 IsEnabled="{Binding !IsBusy}" />

        <!-- ウェブサイトURL -->
        <TextBox Text="{Binding WebsiteUrl, Mode=TwoWay}"
                 Watermark="ウェブサイトURL（任意）"
                 IsEnabled="{Binding !IsBusy}" />

        <!-- 保存ボタン（エラーがある場合は無効化） -->
        <Button Content="保存"
                Command="{Binding SaveCommand}"
                IsEnabled="{Binding !HasErrors}" />

        <!-- バリデーションエラーの有無を表示 -->
        <TextBlock Text="{Binding HasErrors, StringFormat='エラー: {0}'}"
                   Foreground="Red"
                   IsVisible="{Binding HasErrors}" />
    </StackPanel>
</UserControl>
```

### 高度な使用例

```csharp
// バリデーションを一時的に無効化
viewModel.IsValidationEnabled = false;

// 複数のプロパティを一括変更
viewModel.Email = "test@example.com";
viewModel.Age = 25;
viewModel.UserName = "testuser";

// バリデーションを再開（一度だけ実行される）
viewModel.IsValidationEnabled = true;
await viewModel.ValidateAsync();

// 特定プロパティのみバリデーション
await viewModel.ValidatePropertyAsync(nameof(viewModel.Email));

// エラーメッセージを取得
var emailErrors = viewModel.GetPropertyErrors(nameof(viewModel.Email));
foreach (var error in emailErrors)
{
    Console.WriteLine($"Email error: {error}");
}

// すべてのエラーをクリア
viewModel.ClearErrors();
```

---

## 3. FileManagement の自動保存機能

### FileManagerBase の自動保存機能

`FileManagerBase` には自動保存機能が統合されています。`ConfigureAutoSaveEnabled()` をオーバーライドして有効化できます。

#### Step 1: FileManagerBase を継承したクラスを作成

```csharp
using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Serializers;
using llyrtkframework.FileManagement.Backup;
using llyrtkframework.FileManagement.Triggers;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

public class DocumentData
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public DateTime LastModified { get; set; }
}

public class DocumentFileManager : FileManagerBase<DocumentData>
{
    public DocumentFileManager(
        ILogger<DocumentFileManager> logger,
        EventAggregator? eventAggregator = null)
        : base(new JsonFileSerializer<DocumentData>(), logger, eventAggregator)
    {
    }

    protected override string ConfigureFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyApp",
            "document.json");
    }

    protected override BackupOptions ConfigureBackupOptions()
    {
        return new BackupOptions
        {
            MaxBackupCount = 10,
            RetentionPeriod = TimeSpan.FromDays(7),
            BackupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyApp",
                "Backups")
        };
    }

    protected override List<BackupTrigger> ConfigureBackupTriggers()
    {
        return new List<BackupTrigger>
        {
            // 保存時にバックアップ
            new OnSaveBackupTrigger(),
            // 30分間隔でバックアップ
            new IntervalBackupTrigger(TimeSpan.FromMinutes(30))
        };
    }

    /// <summary>
    /// 自動保存機能を有効にする
    /// </summary>
    protected override bool ConfigureAutoSaveEnabled()
    {
        return true; // 自動保存を有効化
    }
}
```

#### Step 2: アプリケーション起動時の初期化

```csharp
using llyrtkframework.FileManagement.Core;
using Microsoft.Extensions.Logging;

// FileManagerRegistry を初期化
var logger = loggerFactory.CreateLogger<FileManagerRegistry>();
var eventAggregator = serviceProvider.GetService<IEventAggregator>();

FileManagerRegistry.Instance.Initialize(logger, eventAggregator);

// 自動保存を開始（500msポーリング）
FileManagerRegistry.Instance.StartAutoSave(TimeSpan.FromMilliseconds(500));

// アプリケーション終了時
// FileManagerRegistry.Instance.StopAutoSave();
```

#### Step 3: ViewModel での使用

```csharp
using llyrtkframework.Mvvm;
using ReactiveUI;

public class DocumentViewModel : ViewModelBase
{
    private readonly DocumentFileManager _fileManager;
    private DocumentData _data;

    private string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _title, value))
            {
                UpdateDataAndMarkChanged();
            }
        }
    }

    private string _content = "";
    public string Content
    {
        get => _content;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _content, value))
            {
                UpdateDataAndMarkChanged();
            }
        }
    }

    public DocumentViewModel(DocumentFileManager fileManager)
    {
        _fileManager = fileManager;
        _data = new DocumentData();

        // ドキュメントをロード
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var result = await _fileManager.LoadAsync();

        if (result.IsSuccess)
        {
            _data = result.Value!;
            Title = _data.Title;
            Content = _data.Content;
        }
        else
        {
            // 新規ドキュメント
            _data = new DocumentData();
        }
    }

    private void UpdateDataAndMarkChanged()
    {
        _data.Title = Title;
        _data.Content = Content;
        _data.LastModified = DateTime.Now;

        // キャッシュを更新し、未保存フラグをセット
        _fileManager.UpdateCachedData(_data);

        // => FileManagerRegistry が500ms後に自動保存
    }

    // 手動保存
    public async Task SaveAsync()
    {
        IsBusy = true;

        try
        {
            UpdateDataAndMarkChanged();
            var result = await _fileManager.SaveAsync(_data);

            if (result.IsSuccess)
            {
                notificationService.ShowSuccess("保存しました");
            }
            else
            {
                notificationService.ShowError($"保存に失敗: {result.ErrorMessage}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### イベント監視

```csharp
using llyrtkframework.Events;
using llyrtkframework.FileManagement.Events;

// 自動保存イベントを監視
eventAggregator.GetEvent<AutoSaveCompletedEvent>()
    .Subscribe(e =>
    {
        Console.WriteLine($"自動保存完了: {e.FilePath} at {e.SavedAt}");
        notificationService.ShowInfo($"自動保存: {Path.GetFileName(e.FilePath)}");
    });

eventAggregator.GetEvent<AutoSaveFailedEvent>()
    .Subscribe(e =>
    {
        Console.WriteLine($"自動保存失敗: {e.FilePath} - {e.ErrorMessage}");
        notificationService.ShowWarning($"自動保存失敗: {e.ErrorMessage}");
    });

eventAggregator.GetEvent<AutoSaveStartedEvent>()
    .Subscribe(e =>
    {
        Console.WriteLine($"自動保存開始: {e.FileCount} files");
    });
```

### 自動保存の制御

```csharp
// グローバル: すべてのFileManagerの自動保存を一時停止（大量更新時など）
FileManagerRegistry.Instance.StopAutoSave();

// 大量のファイル操作
for (int i = 0; i < 1000; i++)
{
    documentViewModel.Title = $"Document {i}";
    // 自動保存されない
}

// 手動保存
await documentViewModel.SaveAsync();

// グローバル: 自動保存を再開
FileManagerRegistry.Instance.StartAutoSave();

// 個別: 特定のFileManagerのみ自動保存を有効化/無効化
fileManager.SetAutoSaveEnabled(false); // 無効化
// ... 処理 ...
fileManager.SetAutoSaveEnabled(true);  // 再開

// 自動保存が有効かどうか確認
if (fileManager.IsAutoSaveEnabled)
{
    Console.WriteLine("自動保存が有効です");
}
```

---

## まとめ

llyrtkframework の新機能により、以下が可能になりました:

1. **PersistentStateManager**: 一時的な状態と永続化された設定の統合管理
2. **ValidatableViewModelBase**: プロパティ変更時の自動バリデーション + UIエラー表示
3. **AutoSavableFileManager + FileManagerRegistry**: 500msポーリングによる自動保存機能

これらの機能を組み合わせることで、堅牢でユーザーフレンドリーなアプリケーションを構築できます。

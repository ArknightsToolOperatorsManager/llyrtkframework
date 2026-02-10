# MVVM Core モジュール

Avalonia MVVMアプリケーションの基盤となるViewModel、コマンド、サービスを提供するモジュールです。

## 概要

MVVM Coreモジュールは、ReactiveUIをベースとしたMVVMパターンの実装を提供します：

- **ViewModelBase**: ReactiveObjectベースの基底クラス
- **コマンドパターン**: 同期/非同期コマンドの実装
- **サービスインターフェース**: ダイアログ、ナビゲーション
- **拡張メソッド**: ReactiveUIの便利な拡張

## 主要コンポーネント

### ViewModelBase

ReactiveObjectを継承した全ViewModelの基底クラス。

```csharp
public class MainViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private int _count;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public int Count
    {
        get => _count;
        set => this.RaiseAndSetIfChanged(ref _count, value);
    }

    // IsBusy, Titleプロパティは基底クラスで提供

    public override void OnInitialize()
    {
        // 初期化処理
    }

    public override void OnActivated()
    {
        // アクティブ時の処理
    }

    public override void OnDeactivated()
    {
        // 非アクティブ時の処理
    }
}
```

#### 提供プロパティ

- **IsBusy**: 処理中フラグ
- **Title**: ビューのタイトル

#### ライフサイクルメソッド

- **OnInitialize()**: ViewModel初期化時
- **OnActivated()**: Viewアクティブ時
- **OnDeactivated()**: View非アクティブ時

### コマンド

#### DelegateCommand

同期処理用のコマンド。

```csharp
public class MyViewModel : ViewModelBase
{
    public DelegateCommand IncrementCommand { get; }
    public DelegateCommand<string> ShowMessageCommand { get; }

    private int _counter;

    public MyViewModel()
    {
        // パラメータなし
        IncrementCommand = new DelegateCommand(
            execute: () => _counter++,
            canExecute: () => _counter < 10
        );

        // パラメータ付き
        ShowMessageCommand = new DelegateCommand<string>(
            execute: message => Console.WriteLine(message),
            canExecute: message => !string.IsNullOrEmpty(message)
        );
    }

    // CanExecuteの再評価
    public void UpdateCounter()
    {
        _counter++;
        IncrementCommand.RaiseCanExecuteChanged();
    }
}
```

#### AsyncDelegateCommand

非同期処理用のコマンド。実行中は自動的にCanExecute=falseになります。

```csharp
public class MyViewModel : ViewModelBase
{
    public AsyncDelegateCommand LoadDataCommand { get; }
    public AsyncDelegateCommand<int> SaveItemCommand { get; }

    private readonly IDataService _dataService;
    private readonly ILogger _logger;

    public MyViewModel(IDataService dataService, ILogger logger)
    {
        _dataService = dataService;
        _logger = logger;

        LoadDataCommand = new AsyncDelegateCommand(
            execute: LoadDataAsync,
            canExecute: () => !IsBusy,
            logger: logger  // エラーログを自動出力
        );

        SaveItemCommand = new AsyncDelegateCommand<int>(
            execute: SaveItemAsync,
            canExecute: id => id > 0,
            logger: logger
        );
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _dataService.LoadAsync();
            // データ処理
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveItemAsync(int? id)
    {
        if (id == null) return;
        await _dataService.SaveAsync(id.Value);
    }
}
```

**特徴**:
- 実行中は自動的に`CanExecute`がfalseになり二重実行を防止
- エラーは自動的にログ出力（loggerを渡した場合）
- `ExecuteAsync()`で明示的な非同期呼び出しも可能

### サービスインターフェース

#### IDialogService

ダイアログ表示のインターフェース（実装はUI層で提供）。

```csharp
public interface IDialogService
{
    Task ShowInformationAsync(string title, string message);
    Task ShowWarningAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task<Result<string>> ShowInputAsync(string title, string message, string defaultValue = "");
    Task<Result<string>> ShowOpenFileDialogAsync(string title, string filter = "All files (*.*)|*.*");
    Task<Result<string>> ShowSaveFileDialogAsync(string title, string defaultFileName = "", string filter = "All files (*.*)|*.*");
    Task<Result<string>> ShowFolderBrowserDialogAsync(string title);
}
```

**使用例**:

```csharp
public class MyViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;

    public AsyncDelegateCommand DeleteCommand { get; }

    public MyViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;

        DeleteCommand = new AsyncDelegateCommand(DeleteAsync);
    }

    private async Task DeleteAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "確認",
            "本当に削除しますか？"
        );

        if (confirmed)
        {
            // 削除処理
            await _dialogService.ShowInformationAsync("完了", "削除しました");
        }
    }
}
```

#### INavigationService

画面遷移のインターフェース（実装はUI層で提供）。

```csharp
public interface INavigationService
{
    Task<Result> NavigateAsync<TViewModel>(object? parameter = null)
        where TViewModel : ViewModelBase;
    Task<Result> NavigateAsync(string viewName, object? parameter = null);
    Task<Result> GoBackAsync();
    bool CanGoBack { get; }
    void ClearHistory();
    ViewModelBase? CurrentViewModel { get; }
}
```

**使用例**:

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public DelegateCommand NavigateToSettingsCommand { get; }
    public DelegateCommand GoBackCommand { get; }

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        NavigateToSettingsCommand = new DelegateCommand(
            () => _navigationService.NavigateAsync<SettingsViewModel>()
        );

        GoBackCommand = new DelegateCommand(
            () => _navigationService.GoBackAsync(),
            () => _navigationService.CanGoBack
        );
    }
}
```

### 拡張メソッド

#### WhenPropertyChanged

プロパティ変更を監視してアクションを実行。

```csharp
public class MyViewModel : ViewModelBase
{
    private string _searchText = string.Empty;
    private IDisposable? _searchSubscription;

    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public MyViewModel()
    {
        // プロパティ変更を監視
        _searchSubscription = this.WhenPropertyChanged(
            vm => vm.SearchText,
            text => Console.WriteLine($"Search: {text}")
        );
    }
}
```

#### WhenPropertyChangedAsync

プロパティ変更を監視して非同期アクションを実行。

```csharp
public class MyViewModel : ViewModelBase
{
    private string _filter = string.Empty;
    private IDisposable? _filterSubscription;

    public string Filter
    {
        get => _filter;
        set => this.RaiseAndSetIfChanged(ref _filter, value);
    }

    public MyViewModel(IDataService dataService)
    {
        // プロパティ変更で非同期処理
        _filterSubscription = this.WhenPropertyChangedAsync(
            vm => vm.Filter,
            async filter => await dataService.FilterAsync(filter)
        );
    }
}
```

#### WhenPropertyChangedThrottled

デバウンス付きでプロパティ変更を監視。

```csharp
public class SearchViewModel : ViewModelBase
{
    private string _query = string.Empty;
    private IDisposable? _searchSubscription;

    public string Query
    {
        get => _query;
        set => this.RaiseAndSetIfChanged(ref _query, value);
    }

    public SearchViewModel(ISearchService searchService)
    {
        // 300ms待ってから検索実行
        _searchSubscription = this.WhenPropertyChangedThrottled(
            vm => vm.Query,
            TimeSpan.FromMilliseconds(300),
            async query => await searchService.SearchAsync(query)
        );
    }
}
```

#### WhenAnyPropertyChanged

任意のプロパティ変更を監視。

```csharp
public class MyViewModel : ViewModelBase
{
    private IDisposable? _changeSubscription;

    public MyViewModel()
    {
        // どのプロパティが変更されても実行
        _changeSubscription = this.WhenAnyPropertyChanged(
            () => Console.WriteLine("Something changed")
        );
    }
}
```

## 使用例

### 基本的なViewModel

```csharp
public class UserViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _email = string.Empty;
    private ObservableCollection<string> _items = new();

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
    }

    public ObservableCollection<string> Items
    {
        get => _items;
        set => this.RaiseAndSetIfChanged(ref _items, value);
    }

    public DelegateCommand AddItemCommand { get; }
    public AsyncDelegateCommand SaveCommand { get; }

    private readonly IUserService _userService;

    public UserViewModel(IUserService userService)
    {
        _userService = userService;
        Title = "ユーザー管理";

        AddItemCommand = new DelegateCommand(
            () => Items.Add($"Item {Items.Count + 1}")
        );

        SaveCommand = new AsyncDelegateCommand(
            SaveAsync,
            () => !string.IsNullOrEmpty(Name)
        );

        // Name変更時にSaveCommandのCanExecuteを更新
        this.WhenPropertyChanged(
            vm => vm.Name,
            _ => SaveCommand.RaiseCanExecuteChanged()
        );
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await _userService.SaveUserAsync(Name, Email);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### DI統合（Prism + DryIoc）

```csharp
// App.xaml.cs
public partial class App : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // サービス登録
        containerRegistry.RegisterSingleton<IDialogService, DialogService>();
        containerRegistry.RegisterSingleton<INavigationService, NavigationService>();
        containerRegistry.RegisterSingleton<IUserService, UserService>();

        // ViewModel登録
        containerRegistry.Register<MainViewModel>();
        containerRegistry.Register<SettingsViewModel>();
    }

    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }
}

// MainWindow.xaml.cs
public MainWindow(MainViewModel viewModel)
{
    InitializeComponent();
    DataContext = viewModel;
    viewModel.OnInitialize();
}
```

### リアクティブなViewModel

```csharp
public class RealtimeViewModel : ViewModelBase
{
    private string _input = string.Empty;
    private string _output = string.Empty;
    private IDisposable? _subscription;

    public string Input
    {
        get => _input;
        set => this.RaiseAndSetIfChanged(ref _input, value);
    }

    public string Output
    {
        get => _output;
        set => this.RaiseAndSetIfChanged(ref _output, value);
    }

    public RealtimeViewModel()
    {
        // Inputが変更されたら500ms後にOutputを更新
        _subscription = this.WhenPropertyChangedThrottled(
            vm => vm.Input,
            TimeSpan.FromMilliseconds(500),
            input => Output = input.ToUpper()
        );
    }

    public override void OnDeactivated()
    {
        _subscription?.Dispose();
    }
}
```

## ベストプラクティス

1. **ViewModelBaseを継承**: すべてのViewModelはViewModelBaseを継承
2. **コマンドの使用**: イベントではなくコマンドでロジックを実装
3. **IsBusyの活用**: 非同期処理中はIsBusyをtrueに
4. **CanExecuteの更新**: 条件変更時は`RaiseCanExecuteChanged()`
5. **購読の解放**: OnDeactivated()でIDisposableをDispose
6. **サービスの注入**: コンストラクタでDI
7. **Titleの設定**: 各ViewModelで適切なタイトルを設定

## ReactiveUIとの統合

このモジュールはReactiveUIをベースにしているため、ReactiveUIの全機能が使用可能です：

```csharp
public class AdvancedViewModel : ViewModelBase
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _fullName = string.Empty;

    public string FirstName
    {
        get => _firstName;
        set => this.RaiseAndSetIfChanged(ref _firstName, value);
    }

    public string LastName
    {
        get => _lastName;
        set => this.RaiseAndSetIfChanged(ref _lastName, value);
    }

    public string FullName
    {
        get => _fullName;
        private set => this.RaiseAndSetIfChanged(ref _fullName, value);
    }

    public AdvancedViewModel()
    {
        // 複数プロパティを監視
        this.WhenAnyValue(
            x => x.FirstName,
            x => x.LastName,
            (first, last) => $"{first} {last}"
        ).Subscribe(full => FullName = full);
    }
}
```

## 他モジュールとの統合

- **Results**: コマンドでResultパターンを使用可能
- **Validation**: FluentValidationとの統合
- **Events**: EventAggregatorでViewModel間通信
- **StateManagement**: グローバル状態との連携

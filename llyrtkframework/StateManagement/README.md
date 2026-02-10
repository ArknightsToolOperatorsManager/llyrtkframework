# State Management モジュール

アプリケーション全体で共有するグローバル状態を管理するモジュールです。

## 概要

State Managementモジュールは、ReactiveUIベースの状態管理を提供します：

- **グローバル状態**: アプリ全体で共有する状態
- **リアクティブ**: 状態変更を自動通知
- **型安全**: ジェネリクスによる型安全性
- **購読可能**: ReactiveUIで状態変更を監視

## 主要コンポーネント

### IStateStore

グローバル状態ストアのインターフェース。

```csharp
public interface IStateStore
{
    Result<T> GetState<T>(string key) where T : class;
    Result SetState<T>(string key, T state) where T : class;
    Result RemoveState(string key);
    bool ContainsState(string key);
    Result Clear();
    IObservable<StateChangedEventArgs> StateChanged { get; }
}
```

### StateStore

状態ストアの実装クラス。

```csharp
// Singleton登録推奨
var stateStore = new StateStore(logger);
```

### ObservableState&lt;T&gt;

ReactiveUIベースの監視可能な状態オブジェクト。

```csharp
public class ObservableState<T> : ReactiveObject
{
    public T Value { get; set; }
    public void Update(Func<T, T> updateFunc);
    public void Reset(T resetValue);
}
```

## 使用例

### 基本的な使用方法

```csharp
var stateStore = new StateStore(logger);

// 状態の設定
var user = new User { Id = 1, Name = "Taro" };
stateStore.SetState("CurrentUser", user);

// 状態の取得
var result = stateStore.GetState<User>("CurrentUser");
if (result.IsSuccess)
{
    Console.WriteLine($"User: {result.Value!.Name}");
}

// 状態の削除
stateStore.RemoveState("CurrentUser");
```

### 状態変更の監視

```csharp
// すべての状態変更を監視
stateStore.StateChanged.Subscribe(e =>
{
    Console.WriteLine($"State changed: {e.Key}");
    Console.WriteLine($"Old: {e.OldValue}");
    Console.WriteLine($"New: {e.NewValue}");
});

// 特定のキーの変更を監視
stateStore.WhenStateChanged("CurrentUser").Subscribe(e =>
{
    Console.WriteLine($"CurrentUser changed to {e.NewValue}");
});

// 型付きで監視
stateStore.WhenStateChanged<User>("CurrentUser").Subscribe(user =>
{
    Console.WriteLine($"User: {user.Name}");
});
```

### ObservableState の使用

```csharp
// カウンターの状態
var counter = new ObservableState<int>(0);

// 値の変更を監視
counter.WhenAnyValue(x => x.Value)
    .Subscribe(value => Console.WriteLine($"Count: {value}"));

// 値の更新
counter.Value = 10;

// 関数で更新
counter.Update(current => current + 1);

// リセット
counter.Reset(0);

// 暗黙的な変換
int currentValue = counter;  // counter.Value と同じ
```

## ViewModelでの使用

### グローバル状態の共有

```csharp
// 状態モデル
public class AppState
{
    public bool IsDarkMode { get; set; }
    public string CurrentLanguage { get; set; } = "en-US";
    public User? CurrentUser { get; set; }
}

// ViewModel 1
public class MainViewModel : ViewModelBase
{
    private readonly IStateStore _stateStore;
    private IDisposable? _subscription;

    public MainViewModel(IStateStore stateStore)
    {
        _stateStore = stateStore;

        // 初期状態を設定
        var appState = new AppState { IsDarkMode = false };
        _stateStore.SetState("AppState", appState);

        // 状態変更を監視
        _subscription = _stateStore
            .WhenStateChanged<AppState>("AppState")
            .Subscribe(state =>
            {
                Console.WriteLine($"Dark Mode: {state.IsDarkMode}");
            });
    }

    public void ToggleDarkMode()
    {
        _stateStore.UpdateState<AppState>("AppState", state =>
        {
            state.IsDarkMode = !state.IsDarkMode;
            return state;
        });
    }

    public override void OnDeactivated()
    {
        _subscription?.Dispose();
    }
}

// ViewModel 2（同じ状態を共有）
public class SettingsViewModel : ViewModelBase
{
    private readonly IStateStore _stateStore;
    private bool _isDarkMode;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isDarkMode, value))
            {
                // 状態を更新
                _stateStore.UpdateState<AppState>("AppState", state =>
                {
                    state.IsDarkMode = value;
                    return state;
                });
            }
        }
    }

    public SettingsViewModel(IStateStore stateStore)
    {
        _stateStore = stateStore;

        // 現在の状態を取得
        var result = _stateStore.GetState<AppState>("AppState");
        if (result.IsSuccess)
        {
            _isDarkMode = result.Value!.IsDarkMode;
        }

        // 他のViewModelからの変更を監視
        _stateStore.WhenStateChanged<AppState>("AppState")
            .Subscribe(state => IsDarkMode = state.IsDarkMode);
    }
}
```

### ユーザー認証状態の管理

```csharp
public class AuthenticationService
{
    private readonly IStateStore _stateStore;

    public IObservable<User> CurrentUserChanged =>
        _stateStore.WhenStateChanged<User>("CurrentUser");

    public AuthenticationService(IStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public async Task<Result> LoginAsync(string username, string password)
    {
        // 認証処理...
        var user = new User { Id = 1, Name = username };

        // グローバル状態に保存
        return _stateStore.SetState("CurrentUser", user);
    }

    public Result Logout()
    {
        return _stateStore.RemoveState("CurrentUser");
    }

    public Result<User> GetCurrentUser()
    {
        return _stateStore.GetState<User>("CurrentUser");
    }
}

// ViewModelでの使用
public class HeaderViewModel : ViewModelBase
{
    private readonly AuthenticationService _authService;
    private string _userName = "Guest";

    public string UserName
    {
        get => _userName;
        set => this.RaiseAndSetIfChanged(ref _userName, value);
    }

    public HeaderViewModel(AuthenticationService authService)
    {
        _authService = authService;

        // ログイン/ログアウトを監視
        _authService.CurrentUserChanged.Subscribe(user =>
        {
            UserName = user.Name;
        });

        // 初期値設定
        var currentUser = _authService.GetCurrentUser();
        if (currentUser.IsSuccess)
        {
            UserName = currentUser.Value!.Name;
        }
    }
}
```

## 拡張メソッド

### GetOrSetState

状態が存在しない場合は自動的に設定。

```csharp
var result = stateStore.GetOrSetState(
    "AppState",
    new AppState { IsDarkMode = false }
);
```

### UpdateState

状態を更新。

```csharp
stateStore.UpdateState<AppState>("AppState", state =>
{
    state.IsDarkMode = !state.IsDarkMode;
    return state;
});
```

### WhenStateChanged

特定キーの状態変更を監視。

```csharp
// イベントとして監視
stateStore.WhenStateChanged("AppState").Subscribe(e =>
{
    Console.WriteLine($"Changed: {e.Key}");
});

// 型付きで監視
stateStore.WhenStateChanged<AppState>("AppState").Subscribe(state =>
{
    Console.WriteLine($"Dark Mode: {state.IsDarkMode}");
});
```

## DI統合

```csharp
// App.xaml.cs
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // Singleton登録
    containerRegistry.RegisterSingleton<IStateStore>(provider =>
    {
        var logger = provider.Resolve<ILogger<StateStore>>();
        var stateStore = new StateStore(logger);

        // 初期状態を設定
        var appState = new AppState
        {
            IsDarkMode = false,
            CurrentLanguage = "en-US"
        };
        stateStore.SetState("AppState", appState);

        return stateStore;
    });

    // AuthenticationServiceなど
    containerRegistry.RegisterSingleton<AuthenticationService>();
}
```

## 複雑な状態管理

### 階層的な状態

```csharp
public class RootState
{
    public UIState UI { get; set; } = new();
    public DataState Data { get; set; } = new();
    public UserState User { get; set; } = new();
}

public class UIState
{
    public bool IsDarkMode { get; set; }
    public string Theme { get; set; } = "Light";
}

public class DataState
{
    public List<Item> Items { get; set; } = new();
    public bool IsLoading { get; set; }
}

public class UserState
{
    public User? CurrentUser { get; set; }
    public List<string> Roles { get; set; } = new();
}

// 使用
stateStore.SetState("Root", new RootState());

stateStore.UpdateState<RootState>("Root", root =>
{
    root.UI.IsDarkMode = true;
    root.User.CurrentUser = new User { Id = 1, Name = "Taro" };
    return root;
});
```

### ViewModel間の通信

```csharp
// データを送信するViewModel
public class ListViewModel : ViewModelBase
{
    private readonly IStateStore _stateStore;

    public DelegateCommand<Item> SelectItemCommand { get; }

    public ListViewModel(IStateStore stateStore)
    {
        _stateStore = stateStore;

        SelectItemCommand = new DelegateCommand<Item>(item =>
        {
            _stateStore.SetState("SelectedItem", item);
        });
    }
}

// データを受信するViewModel
public class DetailViewModel : ViewModelBase
{
    private readonly IStateStore _stateStore;
    private Item? _selectedItem;

    public Item? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public DetailViewModel(IStateStore stateStore)
    {
        _stateStore = stateStore;

        // 選択変更を監視
        _stateStore.WhenStateChanged<Item>("SelectedItem")
            .Subscribe(item => SelectedItem = item);
    }
}
```

## ベストプラクティス

1. **Singleton管理**: IStateStoreはアプリ全体で1インスタンス
2. **キーの定数化**: マジックストリングを避ける
3. **Immutable更新**: 状態は新しいインスタンスで更新
4. **購読の解放**: OnDeactivated()でDispose
5. **型安全性**: ジェネリクスを活用

```csharp
public static class StateKeys
{
    public const string AppState = "AppState";
    public const string CurrentUser = "CurrentUser";
    public const string SelectedItem = "SelectedItem";
}

// 使用
stateStore.SetState(StateKeys.CurrentUser, user);
```

## 他モジュールとの統合

- **MVVM**: ViewModel間の状態共有
- **Events**: EventAggregatorとの併用
- **Configuration**: 永続化が必要な状態はConfigurationへ
- **Results**: エラーハンドリング

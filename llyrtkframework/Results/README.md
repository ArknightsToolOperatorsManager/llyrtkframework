# Results モジュール

例外に頼らない関数型エラーハンドリング（Result<T>パターン）

## 概要

**Result<T>パターン**は、操作の成功/失敗を型安全に表現する設計パターンです。

### メリット:
- ✅ エラーハンドリングが明示的
- ✅ 例外によるパフォーマンス低下を回避
- ✅ Railway Oriented Programming (ROP) 対応
- ✅ 非同期処理との親和性が高い
- ✅ null参照例外を防ぐ

## 基本的な使用方法

### 1. Result（値を返さない操作）

```csharp
using llyrtkframework.Results;

public Result DeleteUser(int userId)
{
    if (userId <= 0)
        return Result.Failure("Invalid user ID");

    try
    {
        _repository.Delete(userId);
        return Result.Success();
    }
    catch (Exception ex)
    {
        return Result.FromException(ex);
    }
}

// 使用側
var result = DeleteUser(123);
if (result.IsSuccess)
{
    Console.WriteLine("User deleted");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### 2. Result<T>（値を返す操作）

```csharp
public Result<User> GetUser(int userId)
{
    if (userId <= 0)
        return Result<User>.Failure("Invalid user ID");

    var user = _repository.FindById(userId);

    return user != null
        ? Result<User>.Success(user)
        : Result<User>.Failure("User not found");
}

// 使用側
var result = GetUser(123);
if (result.IsSuccess)
{
    Console.WriteLine($"User: {result.Value.Name}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

## 高度な使用方法

### 1. Try パターン（例外を自動キャッチ）

```csharp
// 同期版
var result = ResultExtensions.Try(() =>
{
    var data = File.ReadAllText("config.json");
    return JsonSerializer.Deserialize<Config>(data);
});

// 非同期版
var result = await ResultExtensions.TryAsync(async () =>
{
    var response = await _httpClient.GetAsync(url);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
});
```

### 2. Map（値の変換）

```csharp
// ユーザーを取得してDTOに変換
var result = GetUser(123)
    .Map(user => new UserDto
    {
        Id = user.Id,
        FullName = $"{user.FirstName} {user.LastName}"
    });

// 非同期Map
var result = await GetUserAsync(123)
    .MapAsync(async user =>
    {
        var permissions = await GetPermissionsAsync(user.Id);
        return new UserWithPermissions(user, permissions);
    });
```

### 3. Bind（Railway Oriented Programming）

```csharp
// 複数の操作を連鎖
public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
{
    return await ValidateRequest(request)
        .BindAsync(async req => await CreateOrderEntity(req))
        .BindAsync(async order => await SaveToDatabase(order))
        .BindAsync(async order => await SendConfirmationEmail(order));
}

// どこかで失敗したら、以降の処理はスキップされる
```

### 4. OnSuccess / OnFailure（副作用の実行）

```csharp
var result = await SaveUserAsync(user)
    .OnSuccessAsync(async savedUser =>
    {
        await _logger.LogInformation("User saved: {UserId}", savedUser.Id);
        await _eventBus.PublishAsync(new UserCreatedEvent(savedUser));
    })
    .OnFailureAsync(async error =>
    {
        await _logger.LogError("Failed to save user: {Error}", error);
    });
```

### 5. Match（パターンマッチング）

```csharp
var message = GetUser(123).Match(
    onSuccess: user => $"Welcome, {user.Name}!",
    onFailure: error => $"Error: {error}"
);

Console.WriteLine(message);
```

### 6. Ensure（検証の追加）

```csharp
var result = GetUser(123)
    .Ensure(user => user.IsActive, "User is not active")
    .Ensure(user => !user.IsLocked, "User is locked");

// 非同期検証
var result = await GetUserAsync(123)
    .EnsureAsync(async user =>
    {
        var hasPermission = await CheckPermissionAsync(user.Id);
        return hasPermission;
    }, "User does not have permission");
```

### 7. Combine（複数の結果を結合）

```csharp
// すべて成功した場合のみ成功
var result1 = ValidateName(name);
var result2 = ValidateEmail(email);
var result3 = ValidateAge(age);

var combinedResult = Result.Combine(result1, result2, result3);

// すべてのエラーメッセージを含める
var combinedResult = Result.CombineAll(result1, result2, result3);
// Failure: "Invalid name; Invalid email; Invalid age"
```

### 8. TraverseResults（リストの処理）

```csharp
var userIds = new[] { 1, 2, 3, 4, 5 };

// すべてのユーザーを取得、1つでも失敗したら全体が失敗
var result = userIds.TraverseResults(id => GetUser(id));

if (result.IsSuccess)
{
    List<User> users = result.Value;
    Console.WriteLine($"Loaded {users.Count} users");
}

// 非同期版
var result = await userIds.TraverseResultsAsync(async id => await GetUserAsync(id));
```

### 9. Null チェックを Result に変換

```csharp
// Nullable<T>
int? maybeNumber = GetNumber();
var result = maybeNumber.ToResult("Number is null");

// 参照型
User? user = FindUser(id);
var result = user.ToResult("User not found");
```

### 10. 条件付き成功/失敗

```csharp
// 条件が true の場合に成功
var result = Result.SuccessIf(age >= 18, "Must be 18 or older");

// 条件が false の場合に成功
var result = Result.FailureIf(username.Contains("admin"), "Username cannot contain 'admin'");

// 値を返す版
var result = Result<User>.SuccessIf(isValid, user, "User is invalid");
```

## ViewModelでの実践例

```csharp
public class UserViewModel : BindableBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserViewModel> _logger;

    public AsyncRelayCommand LoadUsersCommand { get; }

    public UserViewModel(IUserService userService, ILogger<UserViewModel> logger)
    {
        _userService = userService;
        _logger = logger;
        LoadUsersCommand = new AsyncRelayCommand(LoadUsersAsync);
    }

    private async Task LoadUsersAsync()
    {
        var result = await ResultExtensions.TryAsync(
            async () => await _userService.GetAllUsersAsync(),
            "Failed to load users"
        );

        result
            .OnSuccess(users =>
            {
                Users = new ObservableCollection<User>(users);
                _logger.LogInformation("Loaded {Count} users", users.Count);
            })
            .OnFailure((error, exception) =>
            {
                _logger.LogError(exception, "Error loading users: {Error}", error);
                ShowErrorMessage(error);
            });
    }

    private async Task<Result> SaveUserAsync(User user)
    {
        return await ValidateUser(user)
            .BindAsync(async validUser => await _userService.SaveAsync(validUser))
            .OnSuccessAsync(async savedUser =>
            {
                _logger.LogInformation("User {UserId} saved successfully", savedUser.Id);
                await RefreshUsersAsync();
            })
            .OnFailureAsync(async error =>
            {
                _logger.LogError("Failed to save user: {Error}", error);
                await ShowErrorDialogAsync(error);
            });
    }

    private Result<User> ValidateUser(User user)
    {
        return Result<User>.SuccessIfNotNull(user, "User is null")
            .Ensure(u => !string.IsNullOrEmpty(u.Name), "Name is required")
            .Ensure(u => !string.IsNullOrEmpty(u.Email), "Email is required")
            .Ensure(u => u.Age >= 18, "Must be 18 or older");
    }
}
```

## Pollyとの統合例

```csharp
public class ResilientUserService
{
    private readonly IUserRepository _repository;
    private readonly ILogger<ResilientUserService> _logger;

    public async Task<Result<User>> GetUserWithRetryAsync(int userId)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        return await ResultExtensions.TryAsync(async () =>
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                var user = await _repository.GetByIdAsync(userId);
                return user ?? throw new InvalidOperationException("User not found");
            });
        }, $"Failed to get user {userId} after retries");
    }
}
```

## エラーコードの使用

```csharp
// エラーコード付きで失敗結果を作成
var result = Result.Failure("Invalid credentials", "AUTH_001");

// カスタムエラーコード定義
public static class ErrorCodes
{
    public const string NotFound = "ERR_NOT_FOUND";
    public const string Unauthorized = "ERR_UNAUTHORIZED";
    public const string ValidationFailed = "ERR_VALIDATION";
}

public Result<User> GetUser(int userId)
{
    var user = _repository.FindById(userId);
    return user != null
        ? Result<User>.Success(user)
        : Result<User>.Failure("User not found", ErrorCodes.NotFound);
}

// 使用側
var result = GetUser(123);
if (result.IsFailure && result.ErrorCode == ErrorCodes.NotFound)
{
    // 404を返す
}
```

## ベストプラクティス

### ✅ DO

```csharp
// 明確なエラーメッセージ
return Result.Failure("User with email 'test@example.com' already exists");

// 例外を含める
catch (Exception ex)
{
    return Result.FromException(ex, "Failed to save user");
}

// Railway Oriented Programming を活用
return await ValidateInput(input)
    .BindAsync(ProcessAsync)
    .BindAsync(SaveAsync);
```

### ❌ DON'T

```csharp
// 曖昧なエラーメッセージ
return Result.Failure("Error");

// 失敗結果なのに例外をスロー
if (result.IsFailure)
    throw new Exception(result.ErrorMessage);

// null を返す（Resultを使う意味がない）
public Result<User>? GetUser(int id) => null;
```

## パフォーマンス

- **Result作成**: 数ナノ秒（構造体ではなくクラス）
- **例外との比較**: 例外スローより100倍以上高速
- **メモリ**: 失敗時もオブジェクト1つのみ

## よくある質問

### Q: 例外とResultをどう使い分けるべき？

**A:**
- **Result**: 予期可能なエラー（バリデーション失敗、データ未検出等）
- **例外**: 予期しないエラー（OutOfMemory、StackOverflow等）

### Q: すべてのメソッドをResultに変更すべき？

**A:** いいえ。以下の場合に使用:
- エラーが発生しうる操作
- 呼び出し側がエラー処理をする必要がある操作
- ドメインロジック層

### Q: ASP.NET Core APIで使える？

**A:** はい！

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetUser(int id)
{
    var result = await _userService.GetUserAsync(id);

    return result.Match(
        onSuccess: user => Ok(user),
        onFailure: error => NotFound(new { error })
    );
}
```

## 関連リソース

- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [Functional C#](https://github.com/la-yumba/functional-csharp-code)

# Guard モジュール

防御的プログラミングのためのガード句ヘルパー

## 概要

**Guard**クラスは、メソッドの入力検証を簡潔かつ一貫性を持って行うためのヘルパーメソッド集です。Result<T>パターンと統合されており、例外をスローせずにバリデーションエラーを返します。

### メリット:
- ✅ 明示的なバリデーション
- ✅ 例外を使わないエラーハンドリング
- ✅ Result<T>との完全な統合
- ✅ 一貫性のあるエラーメッセージ
- ✅ コードの可読性向上

## 基本的な使用方法

### 1. Null チェック

```csharp
using llyrtkframework.Guard;
using llyrtkframework.Results;

public Result<User> CreateUser(string? name)
{
    var validationResult = Guard.AgainstNull(name, nameof(name));
    if (validationResult.IsFailure)
        return Result<User>.Failure(validationResult.ErrorMessage!);

    var user = new User { Name = name };
    return Result<User>.Success(user);
}

// より簡潔な書き方（値を取得）
public Result<User> CreateUser(string? name)
{
    return Guard.AgainstNull(name, nameof(name))
        .Map(validName => new User { Name = validName });
}
```

### 2. 文字列の検証

```csharp
// null または空文字列のチェック
public Result SaveUsername(string? username)
{
    return Guard.AgainstNullOrEmpty(username, nameof(username))
        .Bind(_ => _repository.Save(username));
}

// 空白文字のチェック
public Result SaveComment(string? comment)
{
    return Guard.AgainstNullOrWhiteSpace(comment, nameof(comment))
        .Bind(_ => _repository.SaveComment(comment));
}

// 長さの検証
public Result ValidatePassword(string password)
{
    return Guard.Combine(
        Guard.AgainstNullOrEmpty(password, nameof(password)),
        Guard.AgainstMinLength(password, 8, nameof(password)),
        Guard.AgainstMaxLength(password, 128, nameof(password))
    );
}
```

### 3. 数値の検証

```csharp
// 負の数チェック
public Result<Product> CreateProduct(string name, decimal price)
{
    return Guard.Combine(
        Guard.AgainstNullOrEmpty(name, nameof(name)),
        Guard.AgainstNegative(price, nameof(price))
    ).Map(_ => new Product { Name = name, Price = price });
}

// 正の数チェック（ゼロを除く）
public Result ProcessQuantity(int quantity)
{
    return Guard.AgainstNegativeOrZero(quantity, nameof(quantity))
        .Bind(_ => _service.Process(quantity));
}

// 範囲チェック
public Result SetAge(int age)
{
    return Guard.AgainstOutOfRange(age, 0, 150, nameof(age))
        .Bind(_ => UpdateAge(age));
}
```

### 4. コレクションの検証

```csharp
public Result<List<User>> ProcessUsers(IEnumerable<User>? users)
{
    return Guard.AgainstNullOrEmpty(users, nameof(users))
        .Map(_ => users.ToList());
}

// 値を含むバージョン
public Result<decimal> CalculateAverage(IEnumerable<decimal>? values)
{
    return Guard.AgainstNullOrEmptyWithValue(values, nameof(values))
        .Map(v => v.Average());
}
```

### 5. GUID の検証

```csharp
public Result<Order> GetOrder(Guid orderId)
{
    return Guard.AgainstEmptyGuid(orderId, nameof(orderId))
        .Bind(_ => _repository.GetById(orderId));
}

// 値を含むバージョン
public Result<Guid> ValidateOrderId(Guid orderId)
{
    return Guard.AgainstEmptyGuidWithValue(orderId, nameof(orderId));
}
```

### 6. 日付の検証

```csharp
// 過去の日付を許可しない
public Result ScheduleEvent(DateTime eventDate)
{
    return Guard.AgainstPastDate(eventDate, nameof(eventDate))
        .Bind(_ => _calendar.Schedule(eventDate));
}

// 未来の日付を許可しない
public Result RecordHistoricalEvent(DateTime eventDate)
{
    return Guard.AgainstFutureDate(eventDate, nameof(eventDate))
        .Bind(_ => _history.Record(eventDate));
}
```

### 7. カスタム条件

```csharp
public Result UpdateEmail(string email)
{
    var isValidEmail = email.Contains("@") && email.Contains(".");

    return Guard.AgainstCondition(!isValidEmail, "Invalid email format")
        .Bind(_ => _repository.UpdateEmail(email));
}
```

### 8. 複数のガード句を結合

```csharp
public Result<Order> CreateOrder(Guid customerId, IEnumerable<OrderItem>? items, decimal total)
{
    var validationResult = Guard.Combine(
        Guard.AgainstEmptyGuid(customerId, nameof(customerId)),
        Guard.AgainstNullOrEmpty(items, nameof(items)),
        Guard.AgainstNegativeOrZero(total, nameof(total))
    );

    if (validationResult.IsFailure)
        return Result<Order>.Failure(validationResult.ErrorMessage!);

    var order = new Order
    {
        CustomerId = customerId,
        Items = items!.ToList(),
        Total = total
    };

    return Result<Order>.Success(order);
}
```

## 高度な使用方法

### 1. Railway Oriented Programming との統合

```csharp
public async Task<Result<OrderConfirmation>> ProcessOrderAsync(CreateOrderRequest request)
{
    return await ValidateRequest(request)
        .BindAsync(async req => await CreateOrderEntity(req))
        .BindAsync(async order => await CalculateTotal(order))
        .BindAsync(async order => await SaveToDatabase(order))
        .BindAsync(async order => await SendConfirmationEmail(order));
}

private Result ValidateRequest(CreateOrderRequest request)
{
    return Guard.Combine(
        Guard.AgainstNull(request, nameof(request)),
        Guard.AgainstEmptyGuid(request.CustomerId, nameof(request.CustomerId)),
        Guard.AgainstNullOrEmpty(request.Items, nameof(request.Items))
    );
}

private Result<Order> CreateOrderEntity(CreateOrderRequest request)
{
    return Guard.AgainstNullOrEmpty(request.Items, nameof(request.Items))
        .Map(_ => new Order
        {
            CustomerId = request.CustomerId,
            Items = request.Items!.ToList(),
            CreatedAt = DateTime.UtcNow
        });
}
```

### 2. ドメインモデルでの使用

```csharp
public class User
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public int Age { get; private set; }

    private User(string name, string email, int age)
    {
        Name = name;
        Email = email;
        Age = age;
    }

    public static Result<User> Create(string? name, string? email, int age)
    {
        var validationResult = Guard.Combine(
            Guard.AgainstNullOrWhiteSpace(name, nameof(name)),
            Guard.AgainstNullOrWhiteSpace(email, nameof(email)),
            Guard.AgainstOutOfRange(age, 0, 150, nameof(age))
        );

        if (validationResult.IsFailure)
            return Result<User>.Failure(validationResult.ErrorMessage!);

        return Result<User>.Success(new User(name!, email!, age));
    }

    public Result UpdateEmail(string? newEmail)
    {
        return Guard.AgainstNullOrWhiteSpace(newEmail, nameof(newEmail))
            .OnSuccess(_ => Email = newEmail!);
    }
}

// 使用例
var userResult = User.Create("John Doe", "john@example.com", 30);
if (userResult.IsSuccess)
{
    var user = userResult.Value;
    var updateResult = user.UpdateEmail("newemail@example.com");
}
```

### 3. コンストラクタでの使用

```csharp
public class Order
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public List<OrderItem> Items { get; }
    public decimal Total { get; }

    private Order(Guid customerId, List<OrderItem> items, decimal total)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Items = items;
        Total = total;
    }

    public static Result<Order> Create(Guid customerId, IEnumerable<OrderItem>? items)
    {
        var itemsList = items?.ToList();

        var validationResult = Guard.Combine(
            Guard.AgainstEmptyGuid(customerId, nameof(customerId)),
            Guard.AgainstNullOrEmpty(itemsList, nameof(items))
        );

        if (validationResult.IsFailure)
            return Result<Order>.Failure(validationResult.ErrorMessage!);

        var total = itemsList!.Sum(item => item.Price * item.Quantity);

        return Guard.AgainstNegativeOrZero(total, nameof(total))
            .Map(_ => new Order(customerId, itemsList, total));
    }
}
```

### 4. サービス層での使用

```csharp
public class UserService
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UserService> _logger;

    public async Task<Result<User>> CreateUserAsync(string? name, string? email, int age)
    {
        _logger.LogInformation("Creating user: {Name}, {Email}", name, email);

        var validationResult = Guard.Combine(
            Guard.AgainstNullOrWhiteSpace(name, nameof(name)),
            Guard.AgainstNullOrWhiteSpace(email, nameof(email)),
            Guard.AgainstOutOfRange(age, 18, 120, nameof(age))
        );

        if (validationResult.IsFailure)
        {
            _logger.LogWarning("Validation failed: {Error}", validationResult.ErrorMessage);
            return Result<User>.Failure(validationResult.ErrorMessage!);
        }

        return await ResultExtensions.TryAsync(
            async () => await _repository.CreateAsync(new User
            {
                Name = name!,
                Email = email!,
                Age = age
            }),
            "Failed to create user"
        );
    }

    public async Task<Result> UpdateUserEmailAsync(Guid userId, string? newEmail)
    {
        return await Guard.AgainstEmptyGuid(userId, nameof(userId))
            .BindAsync(_ => Guard.AgainstNullOrWhiteSpace(newEmail, nameof(newEmail)))
            .BindAsync(async _ =>
            {
                var user = await _repository.GetByIdAsync(userId);
                return Guard.AgainstNull(user, "user")
                    .Map(u => { u.Email = newEmail!; return u; });
            })
            .BindAsync(async user => await ResultExtensions.TryAsync(
                async () => await _repository.UpdateAsync(user),
                "Failed to update user email"
            ));
    }
}
```

## API リファレンス

### Null チェック
- `AgainstNull(object?, string)` - オブジェクトの null チェック
- `AgainstNull<T>(T?, string)` - 型付き null チェック（値を返す）
- `AgainstNullOrEmpty(string?, string)` - 文字列の null/空チェック
- `AgainstNullOrWhiteSpace(string?, string)` - 文字列の null/空白チェック

### 数値検証
- `AgainstNegative(int/long/decimal/double, string)` - 負の数チェック
- `AgainstNegativeOrZero(int/long/decimal/double, string)` - ゼロまたは負チェック
- `AgainstOutOfRange(number, min, max, string)` - 範囲チェック

### コレクション検証
- `AgainstNullOrEmpty<T>(IEnumerable<T>?, string)` - コレクションの null/空チェック
- `AgainstNullOrEmptyWithValue<T>(IEnumerable<T>?, string)` - コレクション検証（値を返す）

### GUID 検証
- `AgainstEmptyGuid(Guid, string)` - 空 GUID チェック
- `AgainstEmptyGuidWithValue(Guid, string)` - 空 GUID チェック（値を返す）

### 日付検証
- `AgainstPastDate(DateTime, string)` - 過去の日付チェック
- `AgainstFutureDate(DateTime, string)` - 未来の日付チェック

### 文字列長検証
- `AgainstMaxLength(string?, int, string)` - 最大長チェック
- `AgainstMinLength(string?, int, string)` - 最小長チェック
- `AgainstLengthOutOfRange(string?, int, int, string)` - 長さ範囲チェック

### その他
- `AgainstCondition(bool, string)` - カスタム条件チェック
- `Combine(params Result[])` - 複数のガード句を結合

## ベストプラクティス

### ✅ DO

```csharp
// 複数の検証を Combine で結合
var result = Guard.Combine(
    Guard.AgainstNull(user, nameof(user)),
    Guard.AgainstNullOrEmpty(user.Name, nameof(user.Name))
);

// Railway Oriented Programming を活用
return Guard.AgainstNull(userId, nameof(userId))
    .Bind(_ => GetUser(userId))
    .Bind(user => UpdateUser(user));

// ドメインモデルのファクトリメソッドで使用
public static Result<Email> Create(string value)
{
    return Guard.AgainstNullOrWhiteSpace(value, nameof(value))
        .Bind(_ => Guard.AgainstCondition(
            value.Contains("@"),
            "Email must contain @"
        ))
        .Map(_ => new Email(value));
}
```

### ❌ DON'T

```csharp
// 例外をスローしない（Guardの目的に反する）
if (value == null)
    throw new ArgumentNullException(nameof(value));

// Guard の結果を無視しない
Guard.AgainstNull(value, nameof(value)); // 結果を確認していない

// 冗長な検証を避ける
if (value == null)
    return Result.Failure("Value is null");
// 代わりに Guard を使用
return Guard.AgainstNull(value, nameof(value));
```

## よくある質問

### Q: Guard と FluentValidation の使い分けは？

**A:**
- **Guard**: メソッド引数や簡単なドメインルールの検証
- **FluentValidation**: 複雑なビジネスルールや相関検証

### Q: すべての検証を Guard で行うべき？

**A:** いいえ。以下の場合に使用:
- メソッドの入口での引数検証
- ドメインモデルの不変条件チェック
- 簡単な境界値チェック

複雑なビジネスロジックは別途 Validation モジュールや FluentValidation を使用してください。

### Q: パフォーマンスへの影響は？

**A:** 非常に軽量です:
- 例外をスローしないため高速
- 単純な条件分岐のみ
- Result オブジェクトの作成コストは最小限

## 関連モジュール

- [Results](../Results/README.md) - Result<T> パターンの実装
- [Validation](../Validation/README.md) - FluentValidation との統合（今後実装予定）

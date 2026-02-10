# Validation モジュール

FluentValidation と Result パターンの統合

## 概要

**Validation**モジュールは、FluentValidation と Result<T> パターンを統合し、一貫性のあるバリデーションとエラーハンドリングを提供します。

### 含まれるコンポーネント:
- **ValidationExtensions** - FluentValidation と Result の統合拡張メソッド
- **AbstractValidatorBase** - 共通バリデーションルールを提供する基底クラス

### メリット:
- ✅ FluentValidation の強力な検証機能
- ✅ Result パターンとのシームレスな統合
- ✅ Railway Oriented Programming 対応
- ✅ 再利用可能なバリデーションロジック
- ✅ 明示的なエラーハンドリング

## 基本的な使用方法

### 1. シンプルなバリデーター

```csharp
using FluentValidation;
using llyrtkframework.Validation;
using llyrtkframework.Results;

// バリデータークラスの定義
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Valid email address is required");

        RuleFor(x => x.Age)
            .GreaterThanOrEqualTo(18)
            .WithMessage("Must be at least 18 years old")
            .LessThanOrEqualTo(120)
            .WithMessage("Age must be realistic");
    }
}

// 使用例
public class UserService
{
    private readonly IValidator<CreateUserRequest> _validator;
    private readonly IUserRepository _repository;

    public UserService(
        IValidator<CreateUserRequest> validator,
        IUserRepository repository)
    {
        _validator = validator;
        _repository = repository;
    }

    public async Task<Result<User>> CreateUserAsync(CreateUserRequest request)
    {
        // バリデーションを実行し、Result を取得
        var validationResult = await request.ValidateAndReturnAsync(_validator);

        if (validationResult.IsFailure)
            return Result<User>.Failure(validationResult.ErrorMessage!);

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Age = request.Age
        };

        return await ResultExtensions.TryAsync(
            async () => await _repository.CreateAsync(user),
            "Failed to create user"
        );
    }
}
```

### 2. Railway Oriented Programming との統合

```csharp
public async Task<Result<User>> CreateUserAsync(CreateUserRequest request)
{
    return await request.ValidateAndReturnAsync(_validator)
        .BindAsync(async validRequest =>
        {
            var user = new User
            {
                Name = validRequest.Name,
                Email = validRequest.Email,
                Age = validRequest.Age
            };

            return await ResultExtensions.TryAsync(
                async () => await _repository.CreateAsync(user),
                "Failed to create user"
            );
        });
}

// または、より短く
public async Task<Result<User>> CreateUserAsync(CreateUserRequest request)
{
    return await request
        .ValidateAndReturnAsync(_validator)
        .MapAsync(r => new User { Name = r.Name, Email = r.Email, Age = r.Age })
        .BindAsync(async user => await ResultExtensions.TryAsync(
            async () => await _repository.CreateAsync(user),
            "Failed to create user"
        ));
}
```

### 3. AbstractValidatorBase の使用

```csharp
public class CreateOrderRequestValidator : AbstractValidatorBase<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        // Guid の検証（基底クラスのヘルパーメソッド使用）
        RuleFor(x => x.CustomerId)
            .NotEmptyGuid();

        // コレクションの検証
        RuleFor(x => x.Items)
            .NotNull()
            .NotEmpty()
            .WithMessage("Order must have at least one item");

        RuleFor(x => x.Items)
            .MinimumCount(1)
            .MaximumCount(100);

        // 日付の検証
        RuleFor(x => x.DeliveryDate)
            .NotInPast();

        // カスタム検証
        RuleFor(x => x.Total)
            .GreaterThan(0)
            .WithMessage("Total must be greater than 0");
    }
}
```

## 高度な使用方法

### 1. 複雑なバリデーション

```csharp
public class RegisterUserRequestValidator : AbstractValidatorBase<RegisterUserRequest>
{
    private readonly IUserRepository _userRepository;

    public RegisterUserRequestValidator(IUserRepository userRepository)
    {
        _userRepository = userRepository;

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MustAsync(BeUniqueEmail)
            .WithMessage("Email address is already registered");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"\d").WithMessage("Password must contain at least one digit")
            .Matches(@"[\W]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .NotInFuture()
            .Must(BeAtLeast18YearsOld)
            .WithMessage("Must be at least 18 years old");

        RuleFor(x => x.PhoneNumber)
            .IsPhoneNumber()
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }

    private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.FindByEmailAsync(email);
        return existingUser == null;
    }

    private bool BeAtLeast18YearsOld(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;
        return age >= 18;
    }
}
```

### 2. 条件付きバリデーション

```csharp
public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .When(x => x.AccountType == AccountType.Business)
            .WithMessage("Company name is required for business accounts");

        RuleFor(x => x.TaxId)
            .NotEmpty()
            .Matches(@"^\d{9}$")
            .When(x => x.AccountType == AccountType.Business)
            .WithMessage("Valid tax ID is required for business accounts");

        RuleFor(x => x.BillingAddress)
            .NotNull()
            .When(x => x.RequiresBilling)
            .SetValidator(new AddressValidator());

        RuleFor(x => x.ShippingAddress)
            .NotNull()
            .When(x => x.RequiresShipping)
            .SetValidator(new AddressValidator());
    }
}

public class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator()
    {
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.PostalCode).NotEmpty();
        RuleFor(x => x.Country).NotEmpty();
    }
}
```

### 3. カスタムバリデーションルール

```csharp
public class OrderValidator : AbstractValidatorBase<Order>
{
    public OrderValidator()
    {
        RuleFor(x => x)
            .Must(HaveValidTotal)
            .WithMessage("Order total does not match items total");

        RuleFor(x => x.Items)
            .Must(AllItemsBeInStock)
            .WithMessage("Some items are out of stock");

        RuleFor(x => x)
            .Must(BeWithinOrderLimits)
            .WithMessage("Order exceeds maximum allowed amount");
    }

    private bool HaveValidTotal(Order order)
    {
        var calculatedTotal = order.Items.Sum(i => i.Price * i.Quantity);
        return Math.Abs(order.Total - calculatedTotal) < 0.01m;
    }

    private bool AllItemsBeInStock(List<OrderItem> items)
    {
        return items.All(item => item.Product.StockQuantity >= item.Quantity);
    }

    private bool BeWithinOrderLimits(Order order)
    {
        const decimal maxOrderAmount = 100000m;
        return order.Total <= maxOrderAmount;
    }
}
```

### 4. 依存する検証ルール

```csharp
public class PaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    public PaymentRequestValidator()
    {
        // 基本検証
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0");

        // カード支払いの場合の検証
        When(x => x.PaymentMethod == PaymentMethod.CreditCard, () =>
        {
            RuleFor(x => x.CardNumber)
                .NotEmpty()
                .CreditCard()
                .WithMessage("Valid credit card number is required");

            RuleFor(x => x.CardHolderName)
                .NotEmpty()
                .WithMessage("Card holder name is required");

            RuleFor(x => x.ExpiryDate)
                .NotEmpty()
                .GreaterThan(DateTime.UtcNow)
                .WithMessage("Card has expired");

            RuleFor(x => x.CVV)
                .NotEmpty()
                .Matches(@"^\d{3,4}$")
                .WithMessage("Valid CVV is required");
        });

        // 銀行振込の場合の検証
        When(x => x.PaymentMethod == PaymentMethod.BankTransfer, () =>
        {
            RuleFor(x => x.BankAccountNumber)
                .NotEmpty()
                .WithMessage("Bank account number is required");

            RuleFor(x => x.BankName)
                .NotEmpty()
                .WithMessage("Bank name is required");
        });
    }
}
```

### 5. コレクションのバリデーション

```csharp
public class BatchCreateUsersRequestValidator : AbstractValidator<BatchCreateUsersRequest>
{
    public BatchCreateUsersRequestValidator()
    {
        RuleFor(x => x.Users)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one user is required");

        RuleForEach(x => x.Users)
            .SetValidator(new CreateUserRequestValidator());

        RuleFor(x => x.Users)
            .Must(HaveUniqueEmails)
            .WithMessage("Duplicate email addresses found in the batch");
    }

    private bool HaveUniqueEmails(List<CreateUserRequest> users)
    {
        var emails = users.Select(u => u.Email).ToList();
        return emails.Count == emails.Distinct().Count();
    }
}
```

### 6. エラーメッセージのカスタマイズ

```csharp
public class ProductValidator : AbstractValidator<Product>
{
    public ProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("商品名は必須です")
            .MaximumLength(100)
            .WithMessage("商品名は{MaxLength}文字以内で入力してください");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("価格は0円より大きい値を設定してください")
            .WithErrorCode("INVALID_PRICE");

        RuleFor(x => x.Category)
            .NotEmpty()
            .WithMessage("カテゴリを選択してください")
            .WithSeverity(Severity.Error);
    }
}
```

## 実践例

### ASP.NET Core API での使用

```csharp
// Startup.cs または Program.cs
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // FluentValidation の登録
        builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

        var app = builder.Build();
        app.Run();
    }
}

// Controller
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IValidator<CreateUserRequest> _validator;
    private readonly IUserService _userService;

    public UsersController(
        IValidator<CreateUserRequest> validator,
        IUserService userService)
    {
        _validator = validator;
        _userService = userService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await request
            .ValidateAndReturnAsync(_validator)
            .BindAsync(async validRequest => await _userService.CreateUserAsync(validRequest));

        return result.Match(
            onSuccess: user => Ok(user),
            onFailure: error => BadRequest(new { error })
        );
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request)
    {
        var result = await request
            .ValidateAndReturnAsync(_validator)
            .BindAsync(async validRequest =>
                await _userService.UpdateUserAsync(id, validRequest));

        return result.Match(
            onSuccess: user => Ok(user),
            onFailure: error => NotFound(new { error })
        );
    }
}
```

### ViewModel での使用

```csharp
public class UserViewModel : BindableBase
{
    private readonly IValidator<UserModel> _validator;
    private readonly IUserService _userService;

    public AsyncRelayCommand SaveCommand { get; }

    public UserViewModel(
        IValidator<UserModel> validator,
        IUserService userService)
    {
        _validator = validator;
        _userService = userService;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    private async Task SaveAsync()
    {
        var model = new UserModel
        {
            Name = Name,
            Email = Email,
            Age = Age
        };

        var result = await model
            .ValidateAndReturnAsync(_validator)
            .BindAsync(async validModel => await _userService.SaveUserAsync(validModel));

        result
            .OnSuccess(user =>
            {
                ShowSuccessMessage($"User {user.Name} saved successfully");
            })
            .OnFailure(error =>
            {
                ShowErrorMessage(error);
            });
    }
}
```

### 検証結果の詳細な処理

```csharp
public async Task<Result<User>> CreateUserWithDetailedValidation(CreateUserRequest request)
{
    var validationResult = await _validator.ValidateAsync(request);

    if (!validationResult.IsValid)
    {
        // エラーメッセージを辞書形式で取得
        var errorDict = validationResult.GetErrorDictionary();

        // ログ出力
        foreach (var kvp in errorDict)
        {
            _logger.LogWarning("Validation error for {Property}: {Errors}",
                kvp.Key, string.Join(", ", kvp.Value));
        }

        // Result に変換
        return validationResult.ToResult<User>();
    }

    var user = new User
    {
        Name = request.Name,
        Email = request.Email,
        Age = request.Age
    };

    return await ResultExtensions.TryAsync(
        async () => await _repository.CreateAsync(user),
        "Failed to create user"
    );
}
```

## ベストプラクティス

### ✅ DO

```csharp
// バリデータークラスを DI コンテナに登録
services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

// Result パターンと統合
var result = await request.ValidateAndReturnAsync(_validator);

// Railway Oriented Programming を活用
return await request
    .ValidateAndReturnAsync(_validator)
    .BindAsync(ProcessAsync)
    .BindAsync(SaveAsync);

// 複雑なバリデーションは別メソッドに分離
private async Task<bool> BeUniqueEmail(string email, CancellationToken ct)
{
    return await _repository.FindByEmailAsync(email) == null;
}

// エラーメッセージを明確に
RuleFor(x => x.Email)
    .EmailAddress()
    .WithMessage("有効なメールアドレスを入力してください");
```

### ❌ DON'T

```csharp
// try-catch で例外をキャッチしない（Result を使用）
try
{
    var validationResult = _validator.Validate(request);
    // ...
}
catch (ValidationException ex) // NG
{
    // ...
}

// 検証結果を無視しない
await request.ValidateAsync(_validator); // NG: 結果を使用していない

// バリデータークラスを直接 new しない
var validator = new CreateUserRequestValidator(); // NG: DI を使用すべき

// 曖昧なエラーメッセージ
RuleFor(x => x.Email)
    .EmailAddress()
    .WithMessage("Invalid"); // NG: 何が無効か不明確
```

## よくある質問

### Q: FluentValidation と Guard の使い分けは？

**A:**
- **Guard**: メソッド引数の単純なチェック、ドメインロジックの前提条件
- **FluentValidation**: 入力データの複雑な検証、ビジネスルール

両方を組み合わせて使用することも可能です。

### Q: バリデーションのパフォーマンスは？

**A:**
- FluentValidation は非常に高速
- 非同期バリデーション（データベースアクセス等）を使用する場合は注意
- 可能な限り同期的なルールを優先

### Q: カスタムエラーコードを設定できる？

**A:** はい、`WithErrorCode()` を使用できます:
```csharp
RuleFor(x => x.Email)
    .EmailAddress()
    .WithErrorCode("INVALID_EMAIL")
    .WithMessage("Invalid email address");
```

## 関連モジュール

- [Results](../Results/README.md) - Result<T> パターンの実装
- [Guard](../Guard/README.md) - ガード句によるバリデーション
- [Domain](../Domain/README.md) - エンティティと値オブジェクト

## 参考リンク

- [FluentValidation 公式ドキュメント](https://docs.fluentvalidation.net/)

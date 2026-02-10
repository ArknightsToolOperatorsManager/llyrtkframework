# Specification モジュール

仕様パターン（Specification Pattern）の実装

## 概要

**Specification**モジュールは、ビジネスルールやクエリロジックを再利用可能な仕様オブジェクトとしてカプセル化するための仕様パターンを提供します。

### 含まれるコンポーネント:
- **ISpecification<T>** - 仕様のインターフェース
- **Specification<T>** - 仕様の基底クラス
- **CompositeSpecifications** - AND、OR、NOT の複合仕様
- **CommonSpecifications** - 汎用的な仕様（All、None、Expression）

### メリット:
- ✅ ビジネスルールの再利用
- ✅ クエリロジックの一元管理
- ✅ LINQとの統合
- ✅ 読みやすいドメインロジック
- ✅ テスタビリティの向上

## 基本的な使用方法

### 1. シンプルな仕様の作成

```csharp
using llyrtkframework.Specification;
using System.Linq.Expressions;

// アクティブなユーザーの仕様
public class ActiveUserSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.IsActive;
    }
}

// 18歳以上のユーザーの仕様
public class AdultUserSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.Age >= 18;
    }
}

// 使用例
var user = new User { Name = "John", Age = 25, IsActive = true };

var activeSpec = new ActiveUserSpecification();
var adultSpec = new AdultUserSpecification();

Console.WriteLine(activeSpec.IsSatisfiedBy(user)); // True
Console.WriteLine(adultSpec.IsSatisfiedBy(user));  // True
```

### 2. パラメータ付き仕様

```csharp
// 指定された年齢以上のユーザーの仕様
public class MinimumAgeSpecification : Specification<User>
{
    private readonly int _minimumAge;

    public MinimumAgeSpecification(int minimumAge)
    {
        _minimumAge = minimumAge;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.Age >= _minimumAge;
    }
}

// 特定のロールを持つユーザーの仕様
public class UserHasRoleSpecification : Specification<User>
{
    private readonly string _role;

    public UserHasRoleSpecification(string role)
    {
        _role = role;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.Roles.Contains(_role);
    }
}

// 使用例
var minAge20 = new MinimumAgeSpecification(20);
var isAdmin = new UserHasRoleSpecification("Admin");

Console.WriteLine(minAge20.IsSatisfiedBy(user));
Console.WriteLine(isAdmin.IsSatisfiedBy(user));
```

### 3. 仕様の合成（AND、OR、NOT）

```csharp
// メソッドチェーン
var activeAdultSpec = new ActiveUserSpecification()
    .And(new AdultUserSpecification());

var inactiveOrMinorSpec = new ActiveUserSpecification()
    .Not()
    .Or(new AdultUserSpecification().Not());

// 演算子オーバーロード
var spec1 = new ActiveUserSpecification() & new AdultUserSpecification(); // AND
var spec2 = new ActiveUserSpecification() | new AdultUserSpecification(); // OR
var spec3 = !new ActiveUserSpecification(); // NOT

// 使用例
var user1 = new User { Name = "John", Age = 25, IsActive = true };
var user2 = new User { Name = "Jane", Age = 16, IsActive = true };
var user3 = new User { Name = "Bob", Age = 30, IsActive = false };

Console.WriteLine(activeAdultSpec.IsSatisfiedBy(user1)); // True
Console.WriteLine(activeAdultSpec.IsSatisfiedBy(user2)); // False (未成年)
Console.WriteLine(activeAdultSpec.IsSatisfiedBy(user3)); // False (非アクティブ)
```

### 4. LINQクエリでの使用

```csharp
// データベースクエリで使用
var activeAdultSpec = new ActiveUserSpecification()
    .And(new AdultUserSpecification());

// Entity Framework / LINQ to Entities
var users = await dbContext.Users
    .Where(activeAdultSpec.ToExpression())
    .ToListAsync();

// LINQ to Objects
var filteredUsers = userList
    .Where(activeAdultSpec.IsSatisfiedBy)
    .ToList();

// または暗黙的な変換を使用
var users2 = await dbContext.Users
    .Where(activeAdultSpec) // 自動的に Expression に変換される
    .ToListAsync();
```

## 高度な使用方法

### 1. ドメインモデルでの使用

```csharp
public class Order : GuidEntity
{
    public Guid CustomerId { get; private set; }
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }

    // 仕様を使用したビジネスロジック
    public bool CanBeCancelled()
    {
        var spec = new OrderCanBeCancelledSpecification();
        return spec.IsSatisfiedBy(this);
    }

    public bool IsEligibleForRefund()
    {
        var spec = new RefundableOrderSpecification();
        return spec.IsSatisfiedBy(this);
    }
}

// キャンセル可能な注文の仕様
public class OrderCanBeCancelledSpecification : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order =>
            order.Status == OrderStatus.Pending ||
            order.Status == OrderStatus.Processing;
    }
}

// 返金可能な注文の仕様
public class RefundableOrderSpecification : Specification<Order>
{
    private static readonly TimeSpan RefundPeriod = TimeSpan.FromDays(30);

    public override Expression<Func<Order, bool>> ToExpression()
    {
        var cutoffDate = DateTime.UtcNow - RefundPeriod;
        return order =>
            order.Status == OrderStatus.Shipped &&
            order.ShippedAt.HasValue &&
            order.ShippedAt.Value >= cutoffDate;
    }
}
```

### 2. 複雑なビジネスルールの表現

```csharp
// プレミアム顧客の仕様
public class PremiumCustomerSpecification : Specification<Customer>
{
    public override Expression<Func<Customer, bool>> ToExpression()
    {
        return customer =>
            customer.TotalPurchases >= 10000 &&
            customer.MembershipYears >= 2 &&
            customer.IsVerified;
    }
}

// VIP顧客の仕様（プレミアム顧客の拡張）
public class VipCustomerSpecification : Specification<Customer>
{
    public override Expression<Func<Customer, bool>> ToExpression()
    {
        var premiumSpec = new PremiumCustomerSpecification();
        var expression = premiumSpec.ToExpression();

        // プレミアム顧客かつ追加条件
        return customer =>
            expression.Compile()(customer) &&
            customer.TotalPurchases >= 50000;
    }
}

// 割引対象顧客の仕様
public class DiscountEligibleSpecification : Specification<Customer>
{
    private readonly decimal _minimumPurchase;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DiscountEligibleSpecification(
        decimal minimumPurchase,
        IDateTimeProvider dateTimeProvider)
    {
        _minimumPurchase = minimumPurchase;
        _dateTimeProvider = dateTimeProvider;
    }

    public override Expression<Func<Customer, bool>> ToExpression()
    {
        var thirtyDaysAgo = _dateTimeProvider.UtcNow.AddDays(-30);
        var minPurchase = _minimumPurchase;

        return customer =>
            customer.LastPurchaseDate >= thirtyDaysAgo &&
            customer.TotalPurchases >= minPurchase;
    }
}

// 使用例
var premiumSpec = new PremiumCustomerSpecification();
var vipSpec = new VipCustomerSpecification();
var discountSpec = new DiscountEligibleSpecification(1000, dateTimeProvider);

// 複合仕様：VIPまたは割引対象のプレミアム顧客
var targetSpec = vipSpec.Or(premiumSpec.And(discountSpec));

var targetCustomers = await dbContext.Customers
    .Where(targetSpec)
    .ToListAsync();
```

### 3. リポジトリパターンとの統合

```csharp
public interface IRepository<T> where T : class
{
    Task<List<T>> FindAsync(ISpecification<T> specification);
    Task<T?> FindOneAsync(ISpecification<T> specification);
    Task<int> CountAsync(ISpecification<T> specification);
    Task<bool> AnyAsync(ISpecification<T> specification);
}

public class Repository<T> : IRepository<T> where T : class
{
    private readonly DbContext _context;

    public Repository(DbContext context)
    {
        _context = context;
    }

    public async Task<List<T>> FindAsync(ISpecification<T> specification)
    {
        return await _context.Set<T>()
            .Where(specification.ToExpression())
            .ToListAsync();
    }

    public async Task<T?> FindOneAsync(ISpecification<T> specification)
    {
        return await _context.Set<T>()
            .Where(specification.ToExpression())
            .FirstOrDefaultAsync();
    }

    public async Task<int> CountAsync(ISpecification<T> specification)
    {
        return await _context.Set<T>()
            .CountAsync(specification.ToExpression());
    }

    public async Task<bool> AnyAsync(ISpecification<T> specification)
    {
        return await _context.Set<T>()
            .AnyAsync(specification.ToExpression());
    }
}

// 使用例
public class UserService
{
    private readonly IRepository<User> _repository;

    public async Task<List<User>> GetActiveAdultUsersAsync()
    {
        var spec = new ActiveUserSpecification()
            .And(new AdultUserSpecification());

        return await _repository.FindAsync(spec);
    }

    public async Task<int> CountPremiumUsersAsync()
    {
        var spec = new PremiumCustomerSpecification();
        return await _repository.CountAsync(spec);
    }
}
```

### 4. SpecificationBuilder の使用

```csharp
// ラムダ式から仕様を作成
var activeSpec = SpecificationBuilder.Create<User>(u => u.IsActive);
var adultSpec = SpecificationBuilder.Create<User>(u => u.Age >= 18);

// 複合仕様
var complexSpec = activeSpec.And(adultSpec);

// All と None
var allUsers = SpecificationBuilder.All<User>();
var noUsers = SpecificationBuilder.None<User>();

// 使用例
var users = await _repository.FindAsync(
    SpecificationBuilder.Create<User>(u =>
        u.IsActive &&
        u.Age >= 18 &&
        u.Email.Contains("@example.com")
    )
);
```

### 5. 動的な仕様の構築

```csharp
public class UserFilterSpecification : Specification<User>
{
    private readonly UserFilterCriteria _criteria;

    public UserFilterSpecification(UserFilterCriteria criteria)
    {
        _criteria = criteria;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        var spec = SpecificationBuilder.All<User>();

        if (_criteria.IsActive.HasValue)
        {
            spec = spec.And(SpecificationBuilder.Create<User>(
                u => u.IsActive == _criteria.IsActive.Value));
        }

        if (_criteria.MinAge.HasValue)
        {
            spec = spec.And(new MinimumAgeSpecification(_criteria.MinAge.Value));
        }

        if (!string.IsNullOrEmpty(_criteria.Role))
        {
            spec = spec.And(new UserHasRoleSpecification(_criteria.Role));
        }

        if (!string.IsNullOrEmpty(_criteria.EmailDomain))
        {
            spec = spec.And(SpecificationBuilder.Create<User>(
                u => u.Email.EndsWith($"@{_criteria.EmailDomain}")));
        }

        return spec.ToExpression();
    }
}

// 使用例
var criteria = new UserFilterCriteria
{
    IsActive = true,
    MinAge = 18,
    Role = "Admin",
    EmailDomain = "example.com"
};

var filterSpec = new UserFilterSpecification(criteria);
var users = await _repository.FindAsync(filterSpec);
```

### 6. 検証での使用

```csharp
public class CreateOrderValidator
{
    private readonly ISpecification<CreateOrderRequest> _validationSpec;

    public CreateOrderValidator()
    {
        _validationSpec = BuildValidationSpecification();
    }

    private Specification<CreateOrderRequest> BuildValidationSpecification()
    {
        var hasCustomer = SpecificationBuilder.Create<CreateOrderRequest>(
            r => r.CustomerId != Guid.Empty);

        var hasItems = SpecificationBuilder.Create<CreateOrderRequest>(
            r => r.Items != null && r.Items.Any());

        var validTotal = SpecificationBuilder.Create<CreateOrderRequest>(
            r => r.Total > 0);

        return hasCustomer.And(hasItems).And(validTotal);
    }

    public Result Validate(CreateOrderRequest request)
    {
        return _validationSpec.IsSatisfiedBy(request)
            ? Result.Success()
            : Result.Failure("Invalid order request");
    }
}
```

## 実践例

### ECサイトの商品検索

```csharp
// 在庫がある商品
public class InStockSpecification : Specification<Product>
{
    public override Expression<Func<Product, bool>> ToExpression()
    {
        return p => p.StockQuantity > 0;
    }
}

// 価格範囲の商品
public class PriceRangeSpecification : Specification<Product>
{
    private readonly decimal _min;
    private readonly decimal _max;

    public PriceRangeSpecification(decimal min, decimal max)
    {
        _min = min;
        _max = max;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        return p => p.Price >= _min && p.Price <= _max;
    }
}

// カテゴリ別商品
public class CategorySpecification : Specification<Product>
{
    private readonly string _category;

    public CategorySpecification(string category)
    {
        _category = category;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        return p => p.Category == _category;
    }
}

// セール中の商品
public class OnSaleSpecification : Specification<Product>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public OnSaleSpecification(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        var now = _dateTimeProvider.UtcNow;
        return p =>
            p.SaleStartDate.HasValue &&
            p.SaleEndDate.HasValue &&
            p.SaleStartDate.Value <= now &&
            p.SaleEndDate.Value >= now;
    }
}

// 商品検索サービス
public class ProductSearchService
{
    private readonly IRepository<Product> _repository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public async Task<List<Product>> SearchAsync(ProductSearchCriteria criteria)
    {
        var spec = SpecificationBuilder.All<Product>();

        // 在庫あり
        if (criteria.InStockOnly)
        {
            spec = spec.And(new InStockSpecification());
        }

        // 価格範囲
        if (criteria.MinPrice.HasValue || criteria.MaxPrice.HasValue)
        {
            var min = criteria.MinPrice ?? 0;
            var max = criteria.MaxPrice ?? decimal.MaxValue;
            spec = spec.And(new PriceRangeSpecification(min, max));
        }

        // カテゴリ
        if (!string.IsNullOrEmpty(criteria.Category))
        {
            spec = spec.And(new CategorySpecification(criteria.Category));
        }

        // セール中のみ
        if (criteria.OnSaleOnly)
        {
            spec = spec.And(new OnSaleSpecification(_dateTimeProvider));
        }

        return await _repository.FindAsync(spec);
    }
}
```

## ベストプラクティス

### ✅ DO

```csharp
// 仕様に明確な名前をつける
public class ActiveUserSpecification : Specification<User> { }

// 仕様を小さく、単一責任に保つ
public class AdultUserSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.Age >= 18; // シンプルなロジック
    }
}

// 仕様を組み合わせる
var complexSpec = new ActiveUserSpecification()
    .And(new AdultUserSpecification())
    .And(new VerifiedUserSpecification());

// ドメインロジックをカプセル化
public class Order
{
    public bool CanBeShipped()
    {
        var spec = new ShippableOrderSpecification();
        return spec.IsSatisfiedBy(this);
    }
}
```

### ❌ DON'T

```csharp
// 仕様に複雑すぎるロジックを入れない
public class ComplexUserSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        // NG: 複雑すぎる - 分割すべき
        return user =>
            user.IsActive &&
            user.Age >= 18 &&
            user.Email.Contains("@") &&
            user.Roles.Any(r => r == "Admin" || r == "Manager") &&
            user.LastLoginDate > DateTime.UtcNow.AddDays(-30) &&
            user.TotalPurchases > 1000;
    }
}

// 仕様内で外部依存を直接使用しない（コンストラクタで注入）
public class BadSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        var service = new SomeService(); // NG
        return user => service.Check(user);
    }
}
```

## よくある質問

### Q: いつ仕様パターンを使うべき？

**A:** 以下の場合に有効です:
- 複雑なクエリロジックを再利用したい
- ビジネスルールをドメインモデルに含めたい
- 動的なフィルタリングが必要
- テスタビリティを向上させたい

### Q: 仕様とFluentValidationの違いは？

**A:**
- **仕様**: クエリやビジネスルールの判定
- **FluentValidation**: 入力データの検証

両方を組み合わせて使用することも可能です。

### Q: パフォーマンスへの影響は？

**A:**
- Expression ツリーのコンパイルにわずかなオーバーヘッド
- データベースクエリでは Entity Framework が最適化
- メモリ内の評価では `Compile()` がキャッシュされる

## 関連モジュール

- [Domain](../Domain/README.md) - エンティティと値オブジェクト
- [Results](../Results/README.md) - Result<T> パターン
- [Guard](../Guard/README.md) - ガード句によるバリデーション

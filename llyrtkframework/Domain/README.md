# Domain モジュール

ドメイン駆動設計（DDD）の基本構成要素

## 概要

**Domain**モジュールは、ドメイン駆動設計（DDD）における基本的な構成要素を提供します。

### 含まれるコンポーネント:
- **ValueObject** - 値オブジェクトの基底クラス
- **Entity** - エンティティの基底クラス

### メリット:
- ✅ 構造的等価性の自動実装
- ✅ 不変性のサポート
- ✅ ドメインロジックのカプセル化
- ✅ 型安全性の向上
- ✅ ボイラープレートコードの削減

## ValueObject（値オブジェクト）

### 概要

値オブジェクトは、属性によって識別されるドメインオブジェクトです。同じ属性を持つ2つの値オブジェクトは等しいと見なされます。

### 特性:
- **不変性**: 一度作成されたら変更できない
- **構造的等価性**: 属性の値が等しければ等しい
- **副作用のない振る舞い**: メソッドは新しい値オブジェクトを返す

### 基本的な使用方法

#### 1. 複数の値を持つ値オブジェクト

```csharp
using llyrtkframework.Domain;
using llyrtkframework.Results;
using llyrtkframework.Guard;

public class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string postalCode, string country)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }

    public static Result<Address> Create(string? street, string? city, string? postalCode, string? country)
    {
        var validationResult = Guard.Combine(
            Guard.AgainstNullOrWhiteSpace(street, nameof(street)),
            Guard.AgainstNullOrWhiteSpace(city, nameof(city)),
            Guard.AgainstNullOrWhiteSpace(postalCode, nameof(postalCode)),
            Guard.AgainstNullOrWhiteSpace(country, nameof(country))
        );

        if (validationResult.IsFailure)
            return Result<Address>.Failure(validationResult.ErrorMessage!);

        return Result<Address>.Success(new Address(street!, city!, postalCode!, country!));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
    }
}

// 使用例
var address1 = Address.Create("123 Main St", "Tokyo", "100-0001", "Japan");
var address2 = Address.Create("123 Main St", "Tokyo", "100-0001", "Japan");

if (address1.IsSuccess && address2.IsSuccess)
{
    Console.WriteLine(address1.Value == address2.Value); // True（構造的等価性）
}
```

#### 2. 単一の値を持つ値オブジェクト

```csharp
public class Email : SingleValueObject<string>
{
    private Email(string value) : base(value)
    {
    }

    public static Result<Email> Create(string? email)
    {
        return Guard.AgainstNullOrWhiteSpace(email, nameof(email))
            .Bind(_ => Guard.AgainstCondition(
                !email!.Contains("@") || !email.Contains("."),
                "Email must contain @ and ."
            ))
            .Map(_ => new Email(email!));
    }

    public string Domain => Value.Split('@')[1];
}

// 使用例
var emailResult = Email.Create("user@example.com");
if (emailResult.IsSuccess)
{
    var email = emailResult.Value;
    Console.WriteLine(email.Value); // user@example.com
    Console.WriteLine(email.Domain); // example.com

    // 暗黙的な型変換
    string emailString = email; // "user@example.com"
}
```

#### 3. ビジネスロジックを持つ値オブジェクト

```csharp
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string? currency)
    {
        return Guard.Combine(
            Guard.AgainstNegative(amount, nameof(amount)),
            Guard.AgainstNullOrWhiteSpace(currency, nameof(currency))
        ).Map(_ => new Money(amount, currency!.ToUpperInvariant()));
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor)
    {
        return new Money(Amount * factor, Currency);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString()
    {
        return $"{Amount:N2} {Currency}";
    }
}

// 使用例
var price1 = Money.Create(100, "USD").Value;
var price2 = Money.Create(50, "USD").Value;
var total = price1.Add(price2); // 150.00 USD
var taxed = total.Multiply(1.1m); // 165.00 USD
```

#### 4. 複雑な値オブジェクト

```csharp
public class DateRange : ValueObject
{
    public DateTime Start { get; }
    public DateTime End { get; }

    private DateRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public static Result<DateRange> Create(DateTime start, DateTime end)
    {
        if (start > end)
            return Result<DateRange>.Failure("Start date must be before end date");

        return Result<DateRange>.Success(new DateRange(start, end));
    }

    public int DurationInDays => (End - Start).Days;

    public bool Contains(DateTime date)
    {
        return date >= Start && date <= End;
    }

    public bool Overlaps(DateRange other)
    {
        return Start <= other.End && End >= other.Start;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }

    public override string ToString()
    {
        return $"{Start:yyyy-MM-dd} to {End:yyyy-MM-dd}";
    }
}
```

## Entity（エンティティ）

### 概要

エンティティは、識別子によって識別されるドメインオブジェクトです。同じ識別子を持つ2つのエンティティは、属性が異なっていても等しいと見なされます。

### 特性:
- **一意の識別子**: 各エンティティは一意のIDを持つ
- **同一性による等価性**: ID が同じなら等しい
- **ライフサイクル**: 作成、変更、削除のライフサイクルを持つ

### 基本的な使用方法

#### 1. Guid を使用するエンティティ

```csharp
public class User : GuidEntity
{
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private User(string name, Email email) : base()
    {
        Name = name;
        Email = email;
        CreatedAt = DateTime.UtcNow;
    }

    public static Result<User> Create(string? name, string? emailAddress)
    {
        return Guard.AgainstNullOrWhiteSpace(name, nameof(name))
            .Bind(_ => Email.Create(emailAddress))
            .Map(email => new User(name!, email));
    }

    public Result UpdateEmail(string? newEmailAddress)
    {
        return Email.Create(newEmailAddress)
            .OnSuccess(email =>
            {
                Email = email;
                UpdatedAt = DateTime.UtcNow;
            })
            .ToResult();
    }

    public void UpdateName(string? newName)
    {
        var result = Guard.AgainstNullOrWhiteSpace(newName, nameof(newName));
        if (result.IsFailure)
            throw new ArgumentException(result.ErrorMessage);

        Name = newName!;
        UpdatedAt = DateTime.UtcNow;
    }
}

// 使用例
var userResult = User.Create("John Doe", "john@example.com");
if (userResult.IsSuccess)
{
    var user = userResult.Value;
    Console.WriteLine(user.Id); // 自動生成された GUID
    Console.WriteLine(user); // User [Id=...]

    var updateResult = user.UpdateEmail("newemail@example.com");
}
```

#### 2. int を使用するエンティティ

```csharp
public class Product : IntEntity
{
    public string Name { get; private set; }
    public Money Price { get; private set; }
    public int StockQuantity { get; private set; }

    private Product(int id, string name, Money price, int stockQuantity) : base(id)
    {
        Name = name;
        Price = price;
        StockQuantity = stockQuantity;
    }

    public static Result<Product> Create(int id, string? name, Money price, int stockQuantity)
    {
        return Guard.Combine(
            Guard.AgainstNegativeOrZero(id, nameof(id)),
            Guard.AgainstNullOrWhiteSpace(name, nameof(name)),
            Guard.AgainstNull(price, nameof(price)),
            Guard.AgainstNegative(stockQuantity, nameof(stockQuantity))
        ).Map(_ => new Product(id, name!, price, stockQuantity));
    }

    public Result IncreaseStock(int quantity)
    {
        return Guard.AgainstNegativeOrZero(quantity, nameof(quantity))
            .OnSuccess(_ => StockQuantity += quantity);
    }

    public Result DecreaseStock(int quantity)
    {
        if (quantity > StockQuantity)
            return Result.Failure("Insufficient stock");

        StockQuantity -= quantity;
        return Result.Success();
    }
}
```

#### 3. カスタム識別子を使用するエンティティ

```csharp
public class OrderId : SingleValueObject<string>
{
    private OrderId(string value) : base(value)
    {
    }

    public static Result<OrderId> Create(string? value)
    {
        return Guard.AgainstNullOrWhiteSpace(value, nameof(value))
            .Bind(_ => Guard.AgainstCondition(
                value!.Length != 10,
                "OrderId must be 10 characters"
            ))
            .Map(_ => new OrderId(value!));
    }

    public static OrderId Generate()
    {
        var id = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return new OrderId(id);
    }
}

public class Order : Entity<OrderId>
{
    public Guid CustomerId { get; private set; }
    public List<OrderItem> Items { get; private set; }
    public Money Total { get; private set; }
    public DateTime OrderDate { get; private set; }

    private Order(OrderId id, Guid customerId) : base(id)
    {
        CustomerId = customerId;
        Items = new List<OrderItem>();
        OrderDate = DateTime.UtcNow;
        Total = Money.Create(0, "USD").Value;
    }

    public static Result<Order> Create(Guid customerId)
    {
        return Guard.AgainstEmptyGuid(customerId, nameof(customerId))
            .Map(_ => new Order(OrderId.Generate(), customerId));
    }

    public Result AddItem(Product product, int quantity)
    {
        return Guard.AgainstNegativeOrZero(quantity, nameof(quantity))
            .Bind(_ => product.DecreaseStock(quantity))
            .OnSuccess(_ =>
            {
                var item = new OrderItem(product.Id, product.Name, product.Price, quantity);
                Items.Add(item);
                RecalculateTotal();
            });
    }

    private void RecalculateTotal()
    {
        var total = Items
            .Select(item => item.LineTotal)
            .Aggregate((a, b) => a.Add(b));
        Total = total;
    }
}
```

## エンティティと値オブジェクトの使い分け

### 値オブジェクトを使用する場合:
- ✅ 概念的に測定可能、定量化可能、または説明可能
- ✅ 属性のみによって識別される
- ✅ 不変であるべき
- ✅ 他の値と比較可能
- ✅ 例: Email、Money、Address、DateRange

### エンティティを使用する場合:
- ✅ 一意の識別子が必要
- ✅ ライフサイクルを持つ
- ✅ 可変である必要がある
- ✅ 追跡が必要
- ✅ 例: User、Order、Product、Customer

## 実践例

### ドメインモデルの完全な例

```csharp
// 値オブジェクト
public class CustomerId : SingleValueObject<Guid>
{
    private CustomerId(Guid value) : base(value) { }

    public static CustomerId Generate() => new CustomerId(Guid.NewGuid());

    public static Result<CustomerId> Create(Guid id)
    {
        return Guard.AgainstEmptyGuid(id, nameof(id))
            .Map(_ => new CustomerId(id));
    }
}

public class CustomerName : ValueObject
{
    public string FirstName { get; }
    public string LastName { get; }
    public string FullName => $"{FirstName} {LastName}";

    private CustomerName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public static Result<CustomerName> Create(string? firstName, string? lastName)
    {
        return Guard.Combine(
            Guard.AgainstNullOrWhiteSpace(firstName, nameof(firstName)),
            Guard.AgainstNullOrWhiteSpace(lastName, nameof(lastName))
        ).Map(_ => new CustomerName(firstName!, lastName!));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
    }
}

// エンティティ
public class Customer : Entity<CustomerId>
{
    public CustomerName Name { get; private set; }
    public Email Email { get; private set; }
    public Address Address { get; private set; }
    public List<Order> Orders { get; private set; }

    private Customer(CustomerId id, CustomerName name, Email email, Address address)
        : base(id)
    {
        Name = name;
        Email = email;
        Address = address;
        Orders = new List<Order>();
    }

    public static Result<Customer> Create(
        string? firstName,
        string? lastName,
        string? email,
        Address address)
    {
        return CustomerName.Create(firstName, lastName)
            .Bind(name => Email.Create(email)
                .Bind(emailObj => Guard.AgainstNull(address, nameof(address))
                    .Map(_ => new Customer(
                        CustomerId.Generate(),
                        name,
                        emailObj,
                        address
                    ))
                )
            );
    }

    public Result PlaceOrder(Order order)
    {
        Orders.Add(order);
        return Result.Success();
    }
}
```

## ベストプラクティス

### ✅ DO

```csharp
// ファクトリメソッドで検証を行う
public static Result<Email> Create(string? value)
{
    return Guard.AgainstNullOrWhiteSpace(value, nameof(value))
        .Bind(_ => ValidateEmailFormat(value!))
        .Map(_ => new Email(value!));
}

// 値オブジェクトは不変にする
public class Money : ValueObject
{
    public decimal Amount { get; } // private set を使わない
}

// エンティティのプロパティ変更は意味のあるメソッド名で
public Result ChangeAddress(Address newAddress)
{
    Address = newAddress;
    return Result.Success();
}
```

### ❌ DON'T

```csharp
// public setter を持つ値オブジェクト（NG）
public class Email : SingleValueObject<string>
{
    public string Value { get; set; } // NG: 不変性違反
}

// 検証なしのコンストラクタ（NG）
public Email(string value) : base(value) { } // NG: パブリックコンストラクタ

// 意味のないセッター（NG）
public void SetName(string name) // NG: 意図が不明確
{
    Name = name;
}
```

## よくある質問

### Q: ValueObject と record の使い分けは？

**A:**
- **ValueObject**: ドメインロジックと検証が必要な場合
- **record**: 単純なDTOやデータ構造の場合

### Q: エンティティの ID は自動生成すべき？

**A:** ケースバイケース:
- **自動生成**: アプリケーション管理のID（Guid、AutoIncrement）
- **手動指定**: 外部システムのID、ビジネス上のID

### Q: 値オブジェクトをデータベースに保存するには？

**A:** EF Core の例:
```csharp
modelBuilder.Entity<User>()
    .OwnsOne(u => u.Address, a =>
    {
        a.Property(addr => addr.Street).HasColumnName("Street");
        a.Property(addr => addr.City).HasColumnName("City");
    });
```

## 関連モジュール

- [Results](../Results/README.md) - Result<T> パターンの実装
- [Guard](../Guard/README.md) - ガード句によるバリデーション

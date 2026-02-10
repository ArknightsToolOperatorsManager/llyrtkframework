# Data Access Layer モジュール

リポジトリパターンとUnit of Workパターンによるデータアクセス層を提供するモジュールです。

## 概要

Data Access Layerモジュールは、データアクセスの抽象化を提供します：

- **Repository Pattern**: CRUD操作の抽象化
- **Unit of Work**: トランザクション管理
- **Specification Pattern**: 複雑なクエリの表現
- **ページネーション**: 大量データの効率的な取得
- **In-Memory実装**: テストやプロトタイピング用

## 主要コンポーネント

### IRepository&lt;T&gt;

リポジトリパターンのインターフェース。

```csharp
public interface IRepository<T> where T : class
{
    Task<Result<T>> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<Result<T>> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<Result<T>> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<T>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<T>>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
    Task<Result<bool>> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<Result<int>> CountAsync(CancellationToken cancellationToken = default);
}
```

### InMemoryRepository&lt;T&gt;

インメモリのリポジトリ実装（テスト用）。

```csharp
// エンティティ
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// リポジトリ作成
var repository = new InMemoryRepository<User>(
    user => user.Id,  // ID選択関数
    logger
);
```

### IUnitOfWork

Unit of Workパターンのインターフェース。

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<T> GetRepository<T>() where T : class;
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);
    Task<Result> RollbackAsync(CancellationToken cancellationToken = default);
    Task<Result> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
```

## 使用例

### 基本的なCRUD操作

```csharp
var repository = new InMemoryRepository<User>(user => user.Id, logger);

// Create
var newUser = new User { Name = "Taro", Email = "taro@example.com" };
var addResult = await repository.AddAsync(newUser);
if (addResult.IsSuccess)
{
    Console.WriteLine($"Added: {addResult.Value!.Id}");
}

// Read
var getResult = await repository.GetByIdAsync(1);
if (getResult.IsSuccess)
{
    var user = getResult.Value!;
    Console.WriteLine($"User: {user.Name}");
}

// Update
var user = getResult.Value!;
user.Name = "Taro Updated";
await repository.UpdateAsync(user);

// Delete
await repository.DeleteAsync(user);
```

### クエリとフィルタリング

```csharp
// すべて取得
var allResult = await repository.GetAllAsync();

// 条件で検索
var findResult = await repository.FindAsync(u => u.Name.Contains("Taro"));
if (findResult.IsSuccess)
{
    foreach (var user in findResult.Value!)
    {
        Console.WriteLine($"Found: {user.Name}");
    }
}

// 存在チェック
var existsResult = await repository.ExistsAsync(u => u.Email == "taro@example.com");
Console.WriteLine($"Exists: {existsResult.Value}");

// カウント
var countResult = await repository.CountAsync();
Console.WriteLine($"Total users: {countResult.Value}");

// 条件付きカウント
var activeCountResult = await repository.CountAsync(u => u.Email.EndsWith("@example.com"));
```

### Specificationパターン

```csharp
// Specification定義
public class ActiveUserSpecification : Specification<User>
{
    public ActiveUserSpecification()
        : base(user => user.IsActive && !user.IsDeleted)
    {
    }
}

public class EmailDomainSpecification : Specification<User>
{
    private readonly string _domain;

    public EmailDomainSpecification(string domain)
        : base(user => user.Email.EndsWith($"@{domain}"))
    {
        _domain = domain;
    }
}

// 使用
var activeSpec = new ActiveUserSpecification();
var domainSpec = new EmailDomainSpecification("example.com");

// Specification結合
var combinedSpec = activeSpec.And(domainSpec);

var result = await repository.FindAsync(combinedSpec);
```

## 拡張メソッド

### FirstOrDefaultAsync

最初のエンティティを取得。

```csharp
var result = await repository.FirstOrDefaultAsync(u => u.Email == "taro@example.com");
if (result.IsSuccess)
{
    var user = result.Value!;
    Console.WriteLine($"User: {user.Name}");
}
```

### AddRangeAsync

複数エンティティを一括追加。

```csharp
var users = new[]
{
    new User { Name = "User1", Email = "user1@example.com" },
    new User { Name = "User2", Email = "user2@example.com" },
    new User { Name = "User3", Email = "user3@example.com" }
};

var result = await repository.AddRangeAsync(users);
if (result.IsSuccess)
{
    Console.WriteLine($"Added {result.Value!.Count()} users");
}
```

### DeleteRangeAsync

複数エンティティを一括削除。

```csharp
var usersToDelete = await repository.FindAsync(u => u.Email.EndsWith("@old.com"));
if (usersToDelete.IsSuccess)
{
    await repository.DeleteRangeAsync(usersToDelete.Value!);
}
```

### GetPagedAsync

ページネーション。

```csharp
// 1ページ目、10件ずつ
var pagedResult = await repository.GetPagedAsync(pageNumber: 1, pageSize: 10);
if (pagedResult.IsSuccess)
{
    var page = pagedResult.Value!;
    Console.WriteLine($"Page {page.PageNumber}/{page.TotalPages}");
    Console.WriteLine($"Total: {page.TotalCount} items");

    foreach (var item in page.Items)
    {
        Console.WriteLine($"- {item.Name}");
    }

    Console.WriteLine($"Has Previous: {page.HasPreviousPage}");
    Console.WriteLine($"Has Next: {page.HasNextPage}");
}
```

## サービス層での使用

```csharp
public class UserService
{
    private readonly IRepository<User> _repository;
    private readonly ILogger<UserService> _logger;

    public UserService(IRepository<User> repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<User>> CreateUserAsync(string name, string email)
    {
        // バリデーション
        if (string.IsNullOrEmpty(name))
            return Result<User>.Failure("Name is required");

        // 重複チェック
        var existsResult = await _repository.ExistsAsync(u => u.Email == email);
        if (existsResult.IsSuccess && existsResult.Value)
            return Result<User>.Failure("Email already exists");

        // 作成
        var user = new User { Name = name, Email = email };
        return await _repository.AddAsync(user);
    }

    public async Task<Result<User>> GetUserByEmailAsync(string email)
    {
        return await _repository.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<Result<IEnumerable<User>>> SearchUsersAsync(string searchTerm)
    {
        return await _repository.FindAsync(u =>
            u.Name.Contains(searchTerm) || u.Email.Contains(searchTerm)
        );
    }

    public async Task<Result<PagedResult<User>>> GetUsersPagedAsync(int pageNumber, int pageSize)
    {
        return await _repository.GetPagedAsync(pageNumber, pageSize);
    }

    public async Task<Result> UpdateUserAsync(int id, string name, string email)
    {
        var getUserResult = await _repository.GetByIdAsync(id);
        if (getUserResult.IsFailure)
            return Result.Failure(getUserResult.ErrorMessage);

        var user = getUserResult.Value!;
        user.Name = name;
        user.Email = email;

        var updateResult = await _repository.UpdateAsync(user);
        return updateResult.IsSuccess
            ? Result.Success()
            : Result.Failure(updateResult.ErrorMessage);
    }

    public async Task<Result> DeleteUserAsync(int id)
    {
        var getUserResult = await _repository.GetByIdAsync(id);
        if (getUserResult.IsFailure)
            return Result.Failure(getUserResult.ErrorMessage);

        return await _repository.DeleteAsync(getUserResult.Value!);
    }
}
```

## ViewModelでの使用

```csharp
public class UserListViewModel : ViewModelBase
{
    private readonly UserService _userService;
    private ObservableCollection<User> _users = new();
    private int _currentPage = 1;
    private int _totalPages = 1;

    public ObservableCollection<User> Users
    {
        get => _users;
        set => this.RaiseAndSetIfChanged(ref _users, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        set => this.RaiseAndSetIfChanged(ref _totalPages, value);
    }

    public AsyncDelegateCommand LoadUsersCommand { get; }
    public AsyncDelegateCommand<User> DeleteUserCommand { get; }
    public AsyncDelegateCommand NextPageCommand { get; }
    public AsyncDelegateCommand PreviousPageCommand { get; }

    public UserListViewModel(UserService userService)
    {
        _userService = userService;

        LoadUsersCommand = new AsyncDelegateCommand(LoadUsersAsync);
        DeleteUserCommand = new AsyncDelegateCommand<User>(DeleteUserAsync);

        NextPageCommand = new AsyncDelegateCommand(
            async () => await LoadPageAsync(CurrentPage + 1),
            () => CurrentPage < TotalPages
        );

        PreviousPageCommand = new AsyncDelegateCommand(
            async () => await LoadPageAsync(CurrentPage - 1),
            () => CurrentPage > 1
        );
    }

    public override void OnInitialize()
    {
        LoadUsersCommand.ExecuteAsync();
    }

    private async Task LoadUsersAsync()
    {
        await LoadPageAsync(1);
    }

    private async Task LoadPageAsync(int pageNumber)
    {
        IsBusy = true;
        try
        {
            var result = await _userService.GetUsersPagedAsync(pageNumber, 20);
            if (result.IsSuccess)
            {
                var page = result.Value!;
                Users = new ObservableCollection<User>(page.Items);
                CurrentPage = page.PageNumber;
                TotalPages = page.TotalPages;

                NextPageCommand.RaiseCanExecuteChanged();
                PreviousPageCommand.RaiseCanExecuteChanged();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteUserAsync(User? user)
    {
        if (user == null) return;

        var result = await _userService.DeleteUserAsync(user.Id);
        if (result.IsSuccess)
        {
            await LoadUsersAsync();
        }
    }
}
```

## カスタムリポジトリ実装

Entity Frameworkでの実装例：

```csharp
public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;
    private readonly ILogger _logger;

    public EfRepository(DbContext context, ILogger logger)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _logger = logger;
    }

    public async Task<Result<T>> AddAsync(T entity, CancellationToken ct = default)
    {
        try
        {
            var entry = await _dbSet.AddAsync(entity, ct);
            return Result<T>.Success(entry.Entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding entity");
            return Result<T>.FromException(ex, "Error adding entity");
        }
    }

    public async Task<Result<T>> GetByIdAsync(object id, CancellationToken ct = default)
    {
        try
        {
            var entity = await _dbSet.FindAsync(new[] { id }, ct);
            return entity != null
                ? Result<T>.Success(entity)
                : Result<T>.Failure("Entity not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity");
            return Result<T>.FromException(ex, "Error getting entity");
        }
    }

    public async Task<Result<IEnumerable<T>>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        try
        {
            var entities = await _dbSet.Where(predicate).ToListAsync(ct);
            return Result<IEnumerable<T>>.Success(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding entities");
            return Result<IEnumerable<T>>.FromException(ex, "Error finding entities");
        }
    }

    // 他のメソッド実装...
}
```

## DI統合

```csharp
// App.xaml.cs
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // InMemory実装（開発・テスト用）
    containerRegistry.Register(typeof(IRepository<>), typeof(InMemoryRepository<>));

    // サービス登録
    containerRegistry.RegisterSingleton<UserService>();
    containerRegistry.RegisterSingleton<ProductService>();

    // ViewModels
    containerRegistry.Register<UserListViewModel>();
}
```

## ベストプラクティス

1. **Repository per Entity**: エンティティごとにリポジトリを作成
2. **Service層の使用**: Repositoryを直接ViewModelで使わない
3. **Specification活用**: 複雑なクエリはSpecificationで表現
4. **ページネーション**: 大量データは必ずページング
5. **トランザクション**: 複数操作はUnit of Workでまとめる

```csharp
// 良い例
public class OrderService
{
    private readonly IRepository<Order> _orderRepo;
    private readonly IRepository<OrderItem> _orderItemRepo;

    public async Task<Result> CreateOrderAsync(Order order, List<OrderItem> items)
    {
        // 複数のリポジトリ操作をまとめる
        var orderResult = await _orderRepo.AddAsync(order);
        if (orderResult.IsFailure)
            return Result.Failure(orderResult.ErrorMessage);

        var itemResults = await _orderItemRepo.AddRangeAsync(items);
        if (itemResults.IsFailure)
            return Result.Failure(itemResults.ErrorMessage);

        return Result.Success();
    }
}

// 悪い例（ViewModelから直接Repository）
public class BadViewModel : ViewModelBase
{
    private readonly IRepository<User> _repository;  // NG

    // Serviceを経由すべき
}
```

## 他モジュールとの統合

- **Specification**: 複雑なクエリの表現
- **Results**: 一貫したエラーハンドリング
- **Logging**: データアクセスのログ記録
- **Validation**: エンティティのバリデーション
- **MVVM**: Service層を介したViewModel統合

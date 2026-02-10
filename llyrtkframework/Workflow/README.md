# Workflow モジュール

複雑なビジネスロジックをパイプライン形式で処理するモジュールです。

## 概要

Workflowモジュールは、パイプラインパターンによる処理フローを提供します：

- **Pipeline**: 複数のステップを連結して処理
- **Fluent API**: 直感的なパイプライン構築
- **型変換**: ステップ間で型を変換可能
- **エラーハンドリング**: Resultパターンで統一
- **非同期処理**: 全ステップが非同期対応

## 主要コンポーネント

### IPipeline&lt;TInput, TOutput&gt;

パイプラインのインターフェース。

```csharp
public interface IPipeline<TInput, TOutput>
{
    Task<Result<TOutput>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default
    );
}
```

### Pipeline&lt;TInput, TOutput&gt;

パイプラインの実装クラス。

```csharp
var pipeline = new Pipeline<string, int>(logger);

pipeline
    .AddStep("Parse", async (string input, ct) =>
    {
        if (int.TryParse(input, out var result))
            return Result<int>.Success(result);
        return Result<int>.Failure("Parse failed");
    })
    .AddStep("Validate", async (int value, ct) =>
    {
        if (value > 0)
            return Result<int>.Success(value);
        return Result<int>.Failure("Value must be positive");
    });

var result = await pipeline.ExecuteAsync("42");
```

### IPipelineStep&lt;TInput, TOutput&gt;

パイプラインステップのインターフェース。

```csharp
public interface IPipelineStep<TInput, TOutput>
{
    string Name { get; }
    Task<Result<TOutput>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default
    );
}
```

### PipelineBuilder

Fluent APIでパイプラインを構築。

```csharp
var pipeline = PipelineBuilder
    .Create(logger)
    .Build<string, ProcessedData>();
```

## 使用例

### 基本的なパイプライン

```csharp
// データ処理パイプライン
var pipeline = new Pipeline<string, ProcessedData>(logger);

pipeline
    .AddStep("LoadData", LoadDataAsync)
    .AddStep("ValidateData", ValidateDataAsync)
    .AddStep("TransformData", TransformDataAsync)
    .AddStep("SaveData", SaveDataAsync);

// 実行
var result = await pipeline.ExecuteAsync("input.json");
if (result.IsSuccess)
{
    Console.WriteLine($"Processed: {result.Value!.Id}");
}

// ステップ関数
async Task<Result<RawData>> LoadDataAsync(string filePath, CancellationToken ct)
{
    var data = await File.ReadAllTextAsync(filePath, ct);
    return Result<RawData>.Success(JsonSerializer.Deserialize<RawData>(data)!);
}

async Task<Result<ValidData>> ValidateDataAsync(RawData data, CancellationToken ct)
{
    if (string.IsNullOrEmpty(data.Name))
        return Result<ValidData>.Failure("Name is required");

    return Result<ValidData>.Success(new ValidData { Name = data.Name });
}

async Task<Result<TransformedData>> TransformDataAsync(ValidData data, CancellationToken ct)
{
    var transformed = new TransformedData
    {
        Name = data.Name.ToUpper(),
        Timestamp = DateTime.Now
    };
    return Result<TransformedData>.Success(transformed);
}

async Task<Result<ProcessedData>> SaveDataAsync(TransformedData data, CancellationToken ct)
{
    // 保存処理
    var processed = new ProcessedData { Id = Guid.NewGuid(), Data = data };
    return Result<ProcessedData>.Success(processed);
}
```

### カスタムステップクラス

```csharp
public class ValidationStep : PipelineStep<UserInput, ValidatedUser>
{
    private readonly IValidator<UserInput> _validator;

    public override string Name => "UserValidation";

    public ValidationStep(IValidator<UserInput> validator)
    {
        _validator = validator;
    }

    public override async Task<Result<ValidatedUser>> ExecuteAsync(
        UserInput input,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors);
            return Result<ValidatedUser>.Failure($"Validation failed: {errors}");
        }

        return Result<ValidatedUser>.Success(new ValidatedUser
        {
            Email = input.Email,
            Name = input.Name
        });
    }
}

// 使用
var pipeline = new Pipeline<UserInput, SavedUser>(logger);
pipeline.AddStep(new ValidationStep(validator));
```

### ビジネスロジックのパイプライン化

```csharp
// 注文処理パイプライン
public class OrderProcessingPipeline
{
    private readonly Pipeline<OrderRequest, OrderResult> _pipeline;

    public OrderProcessingPipeline(
        IInventoryService inventory,
        IPaymentService payment,
        IShippingService shipping,
        ILogger logger)
    {
        _pipeline = new Pipeline<OrderRequest, OrderResult>(logger);

        _pipeline
            .AddStep("ValidateOrder", ValidateOrderAsync)
            .AddStep("CheckInventory", async (order, ct) =>
            {
                var available = await inventory.CheckAvailabilityAsync(order.Items, ct);
                if (!available)
                    return Result<OrderRequest>.Failure("Items not available");
                return Result<OrderRequest>.Success(order);
            })
            .AddStep("ProcessPayment", async (order, ct) =>
            {
                var paymentResult = await payment.ProcessAsync(order.PaymentInfo, ct);
                if (paymentResult.IsFailure)
                    return Result<PaidOrder>.Failure(paymentResult.ErrorMessage);

                return Result<PaidOrder>.Success(new PaidOrder
                {
                    OrderId = order.Id,
                    TransactionId = paymentResult.Value!
                });
            })
            .AddStep("ArrangeShipping", async (paidOrder, ct) =>
            {
                var shippingId = await shipping.ArrangeAsync(paidOrder.OrderId, ct);
                return Result<OrderResult>.Success(new OrderResult
                {
                    OrderId = paidOrder.OrderId,
                    TransactionId = paidOrder.TransactionId,
                    ShippingId = shippingId
                });
            });
    }

    public async Task<Result<OrderResult>> ProcessAsync(OrderRequest request)
    {
        return await _pipeline.ExecuteAsync(request);
    }

    private async Task<Result<OrderRequest>> ValidateOrderAsync(
        OrderRequest order,
        CancellationToken ct)
    {
        if (order.Items.Count == 0)
            return Result<OrderRequest>.Failure("No items in order");

        if (order.PaymentInfo == null)
            return Result<OrderRequest>.Failure("Payment info required");

        return Result<OrderRequest>.Success(order);
    }
}

// 使用
var orderPipeline = new OrderProcessingPipeline(inventory, payment, shipping, logger);
var result = await orderPipeline.ProcessAsync(orderRequest);
```

### Resilienceとの統合

```csharp
using llyrtkframework.Resilience;

var pipeline = new Pipeline<string, ProcessedData>(logger);

pipeline.AddStep("LoadWithRetry", async (string path, ct) =>
{
    // リトライ付きで実行
    return await RetryExtensions.ExecuteWithRetryAsync(
        async () =>
        {
            var data = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<RawData>(data)!;
        },
        maxRetryAttempts: 3,
        initialDelay: TimeSpan.FromSeconds(1),
        logger: logger
    );
});
```

### 並列パイプライン

```csharp
// 複数のパイプラインを並列実行
public class ParallelProcessor
{
    private readonly Pipeline<string, DataA> _pipelineA;
    private readonly Pipeline<string, DataB> _pipelineB;
    private readonly Pipeline<string, DataC> _pipelineC;

    public ParallelProcessor(ILogger logger)
    {
        _pipelineA = new Pipeline<string, DataA>(logger);
        _pipelineB = new Pipeline<string, DataB>(logger);
        _pipelineC = new Pipeline<string, DataC>(logger);

        // 各パイプライン設定...
    }

    public async Task<Result<CombinedResult>> ProcessAsync(string input)
    {
        // 並列実行
        var tasks = new[]
        {
            _pipelineA.ExecuteAsync(input),
            _pipelineB.ExecuteAsync(input),
            _pipelineC.ExecuteAsync(input)
        };

        var results = await Task.WhenAll(tasks);

        // すべて成功しているかチェック
        if (results.Any(r => r.IsFailure))
        {
            var errors = string.Join("; ", results.Where(r => r.IsFailure)
                .Select(r => r.ErrorMessage));
            return Result<CombinedResult>.Failure(errors);
        }

        return Result<CombinedResult>.Success(new CombinedResult
        {
            A = results[0].Value!,
            B = results[1].Value!,
            C = results[2].Value!
        });
    }
}
```

### 条件分岐パイプライン

```csharp
public class ConditionalPipeline
{
    private readonly Pipeline<InputData, OutputData> _pipeline;

    public ConditionalPipeline(ILogger logger)
    {
        _pipeline = new Pipeline<InputData, OutputData>(logger);

        _pipeline
            .AddStep("Classify", async (input, ct) =>
            {
                // 分類
                input.Category = ClassifyData(input);
                return Result<InputData>.Success(input);
            })
            .AddStep("ProcessByCategory", async (input, ct) =>
            {
                // カテゴリに応じた処理
                return input.Category switch
                {
                    "TypeA" => await ProcessTypeAAsync(input, ct),
                    "TypeB" => await ProcessTypeBAsync(input, ct),
                    "TypeC" => await ProcessTypeCAsync(input, ct),
                    _ => Result<ProcessedData>.Failure($"Unknown category: {input.Category}")
                };
            })
            .AddStep("Finalize", FinalizeAsync);
    }

    private string ClassifyData(InputData input)
    {
        // 分類ロジック
        if (input.Value > 100) return "TypeA";
        if (input.Value > 50) return "TypeB";
        return "TypeC";
    }

    private async Task<Result<ProcessedData>> ProcessTypeAAsync(
        InputData input, CancellationToken ct)
    {
        // TypeA専用の処理
        return Result<ProcessedData>.Success(new ProcessedData());
    }

    // 他のProcessTypeXAsyncメソッド...

    private async Task<Result<OutputData>> FinalizeAsync(
        ProcessedData processed, CancellationToken ct)
    {
        return Result<OutputData>.Success(new OutputData());
    }
}
```

## ViewModelでの使用

```csharp
public class DataProcessViewModel : ViewModelBase
{
    private readonly Pipeline<string, ProcessedData> _pipeline;

    public AsyncDelegateCommand<string> ProcessCommand { get; }

    public DataProcessViewModel(ILogger logger)
    {
        _pipeline = new Pipeline<string, ProcessedData>(logger);

        _pipeline
            .AddStep("Load", LoadAsync)
            .AddStep("Validate", ValidateAsync)
            .AddStep("Process", ProcessAsync);

        ProcessCommand = new AsyncDelegateCommand<string>(
            async filePath => await ExecutePipelineAsync(filePath!)
        );
    }

    private async Task ExecutePipelineAsync(string filePath)
    {
        IsBusy = true;
        try
        {
            var result = await _pipeline.ExecuteAsync(filePath);

            if (result.IsSuccess)
            {
                // 成功時の処理
                Console.WriteLine($"Success: {result.Value!.Id}");
            }
            else
            {
                // エラー表示
                Console.WriteLine($"Error: {result.ErrorMessage}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ステップメソッド...
}
```

## ベストプラクティス

1. **ステップの単一責任**: 各ステップは1つの責務のみ
2. **適切な粒度**: ステップは細かすぎず、粗すぎず
3. **明確な命名**: ステップ名は処理内容を明確に
4. **エラーハンドリング**: 各ステップでResultパターンを使用
5. **キャンセレーション**: CancellationTokenを適切に伝播
6. **ログ記録**: パイプライン全体の処理をログに記録

```csharp
// 良い例
pipeline
    .AddStep("LoadUserData", LoadUserDataAsync)
    .AddStep("ValidateAge", ValidateAgeAsync)
    .AddStep("CheckPermissions", CheckPermissionsAsync)
    .AddStep("SaveToDatabase", SaveToDatabaseAsync);

// 悪い例（粒度が細かすぎる）
pipeline
    .AddStep("OpenFile", ...)
    .AddStep("ReadBytes", ...)
    .AddStep("ConvertToString", ...)
    .AddStep("ParseJson", ...);
```

## 他モジュールとの統合

- **Results**: 全ステップでResultパターン使用
- **Resilience**: リトライ機能との統合
- **Logging**: パイプライン実行のログ記録
- **Validation**: FluentValidationとの統合
- **MVVM**: ViewModelでのビジネスロジック処理

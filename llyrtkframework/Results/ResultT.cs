namespace llyrtkframework.Results;

/// <summary>
/// 値を返す操作の結果を表すクラス
/// </summary>
/// <typeparam name="T">返す値の型</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// 結果の値（成功時のみ有効）
    /// </summary>
    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {ErrorMessage}");

            return _value!;
        }
    }

    protected Result(bool isSuccess, T? value, string? errorMessage, Exception? exception = null, string? errorCode = null)
        : base(isSuccess, errorMessage, exception, errorCode)
    {
        if (isSuccess && value == null)
            throw new InvalidOperationException("Success result must have a value.");

        _value = value;
    }

    /// <summary>
    /// 成功結果を作成します
    /// </summary>
    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 失敗結果を作成します
    /// </summary>
    public static new Result<T> Failure(string errorMessage)
    {
        return new Result<T>(false, default, errorMessage);
    }

    /// <summary>
    /// 失敗結果を作成します（例外付き）
    /// </summary>
    public static new Result<T> Failure(string errorMessage, Exception exception)
    {
        return new Result<T>(false, default, errorMessage, exception);
    }

    /// <summary>
    /// 失敗結果を作成します（エラーコード付き）
    /// </summary>
    public static new Result<T> Failure(string errorMessage, string errorCode)
    {
        return new Result<T>(false, default, errorMessage, null, errorCode);
    }

    /// <summary>
    /// 失敗結果を作成します（例外とエラーコード付き）
    /// </summary>
    public static new Result<T> Failure(string errorMessage, Exception exception, string errorCode)
    {
        return new Result<T>(false, default, errorMessage, exception, errorCode);
    }

    /// <summary>
    /// 例外から失敗結果を作成します
    /// </summary>
    public static new Result<T> FromException(Exception exception, string? customMessage = null)
    {
        var message = customMessage ?? exception.Message;
        return new Result<T>(false, default, message, exception);
    }

    /// <summary>
    /// 値が null でない場合に成功結果を返します
    /// </summary>
    public static Result<T> SuccessIfNotNull(T? value, string errorMessage)
    {
        return value != null ? Success(value) : Failure(errorMessage);
    }

    /// <summary>
    /// 条件によって成功または失敗の結果を返します
    /// </summary>
    public static Result<T> SuccessIf(bool condition, T value, string errorMessage)
    {
        return condition ? Success(value) : Failure(errorMessage);
    }

    /// <summary>
    /// 条件によって失敗または成功の結果を返します
    /// </summary>
    public static Result<T> FailureIf(bool condition, T value, string errorMessage)
    {
        return condition ? Failure(errorMessage) : Success(value);
    }

    /// <summary>
    /// Result から Result&lt;T&gt; に変換します（値を指定）
    /// </summary>
    public static Result<T> From(Result result, T value)
    {
        return result.IsSuccess
            ? Success(value)
            : new Result<T>(false, default, result.ErrorMessage, result.Exception, result.ErrorCode);
    }

    /// <summary>
    /// 値を変換します（成功時のみ）
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(ErrorMessage!, Exception!, ErrorCode!);

        try
        {
            var newValue = mapper(Value);
            return Result<TNew>.Success(newValue);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure("Mapping failed", ex);
        }
    }

    /// <summary>
    /// 値を非同期に変換します（成功時のみ）
    /// </summary>
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(ErrorMessage!, Exception!, ErrorCode!);

        try
        {
            var newValue = await mapper(Value);
            return Result<TNew>.Success(newValue);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure("Async mapping failed", ex);
        }
    }

    /// <summary>
    /// 連鎖的に Result を返す処理を実行します（Railway Oriented Programming）
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        if (IsFailure)
            return Result<TNew>.Failure(ErrorMessage!, Exception!, ErrorCode!);

        try
        {
            return binder(Value);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure("Binding failed", ex);
        }
    }

    /// <summary>
    /// 連鎖的に Result を返す非同期処理を実行します
    /// </summary>
    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder)
    {
        if (IsFailure)
            return Result<TNew>.Failure(ErrorMessage!, Exception!, ErrorCode!);

        try
        {
            return await binder(Value);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure("Async binding failed", ex);
        }
    }

    /// <summary>
    /// 成功時のみアクションを実行します
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
        {
            action(Value);
        }
        return this;
    }

    /// <summary>
    /// 失敗時のみアクションを実行します
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure)
        {
            action(ErrorMessage!);
        }
        return this;
    }

    /// <summary>
    /// 失敗時のみアクションを実行します（例外も渡す）
    /// </summary>
    public Result<T> OnFailure(Action<string, Exception?> action)
    {
        if (IsFailure)
        {
            action(ErrorMessage!, Exception);
        }
        return this;
    }

    /// <summary>
    /// 成功時と失敗時でそれぞれ処理を実行します
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(ErrorMessage!);
    }

    /// <summary>
    /// 値を取得するか、失敗時はデフォルト値を返します
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value : defaultValue;
    }

    /// <summary>
    /// 値を取得するか、失敗時は関数を実行してデフォルト値を返します
    /// </summary>
    public T GetValueOrDefault(Func<T> defaultValueProvider)
    {
        return IsSuccess ? Value : defaultValueProvider();
    }

    /// <summary>
    /// 暗黙的に T に変換します（失敗時は例外をスロー）
    /// </summary>
    public static implicit operator T(Result<T> result)
    {
        return result.Value;
    }

    public override string ToString()
    {
        return IsSuccess
            ? $"Success: {Value}"
            : $"Failure: {ErrorMessage}" + (ErrorCode != null ? $" (Code: {ErrorCode})" : "");
    }
}

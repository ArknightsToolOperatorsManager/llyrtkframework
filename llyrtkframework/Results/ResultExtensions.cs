namespace llyrtkframework.Results;

/// <summary>
/// Result および Result&lt;T&gt; の拡張メソッド
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// try-catch を Result で包みます
    /// </summary>
    public static Result Try(Action action, string? errorMessage = null)
    {
        try
        {
            action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(errorMessage ?? ex.Message, ex);
        }
    }

    /// <summary>
    /// try-catch を Result&lt;T&gt; で包みます
    /// </summary>
    public static Result<T> Try<T>(Func<T> func, string? errorMessage = null)
    {
        try
        {
            var value = func();
            return Result<T>.Success(value);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(errorMessage ?? ex.Message, ex);
        }
    }

    /// <summary>
    /// 非同期 try-catch を Result で包みます
    /// </summary>
    public static async Task<Result> TryAsync(Func<Task> func, string? errorMessage = null)
    {
        try
        {
            await func();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(errorMessage ?? ex.Message, ex);
        }
    }

    /// <summary>
    /// 非同期 try-catch を Result&lt;T&gt; で包みます
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func, string? errorMessage = null)
    {
        try
        {
            var value = await func();
            return Result<T>.Success(value);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(errorMessage ?? ex.Message, ex);
        }
    }

    /// <summary>
    /// Task&lt;Result&gt; に対して OnSuccess を実行します
    /// </summary>
    public static async Task<Result> OnSuccessAsync(this Task<Result> resultTask, Func<Task> action)
    {
        var result = await resultTask;
        if (result.IsSuccess)
        {
            await action();
        }
        return result;
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して OnSuccess を実行します
    /// </summary>
    public static async Task<Result<T>> OnSuccessAsync<T>(this Task<Result<T>> resultTask, Func<T, Task> action)
    {
        var result = await resultTask;
        if (result.IsSuccess)
        {
            await action(result.Value);
        }
        return result;
    }

    /// <summary>
    /// Task&lt;Result&gt; に対して OnFailure を実行します
    /// </summary>
    public static async Task<Result> OnFailureAsync(this Task<Result> resultTask, Func<string, Task> action)
    {
        var result = await resultTask;
        if (result.IsFailure)
        {
            await action(result.ErrorMessage!);
        }
        return result;
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して OnFailure を実行します
    /// </summary>
    public static async Task<Result<T>> OnFailureAsync<T>(this Task<Result<T>> resultTask, Func<string, Task> action)
    {
        var result = await resultTask;
        if (result.IsFailure)
        {
            await action(result.ErrorMessage!);
        }
        return result;
    }

    /// <summary>
    /// 複数の Result&lt;T&gt; を結合して、すべての値をリストで返します
    /// </summary>
    public static Result<List<T>> Combine<T>(this IEnumerable<Result<T>> results)
    {
        var resultList = results.ToList();
        var failures = resultList.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
            return Result<List<T>>.Failure(errorMessages);
        }

        var values = resultList.Select(r => r.Value).ToList();
        return Result<List<T>>.Success(values);
    }

    /// <summary>
    /// IEnumerable&lt;T&gt; の各要素に対して Result&lt;TResult&gt; を返す関数を適用し、すべて成功した場合のみリストを返します
    /// </summary>
    public static Result<List<TResult>> TraverseResults<T, TResult>(
        this IEnumerable<T> source,
        Func<T, Result<TResult>> selector)
    {
        var results = source.Select(selector).ToList();
        return results.Combine();
    }

    /// <summary>
    /// IEnumerable&lt;T&gt; の各要素に対して非同期で Result&lt;TResult&gt; を返す関数を適用します
    /// </summary>
    public static async Task<Result<List<TResult>>> TraverseResultsAsync<T, TResult>(
        this IEnumerable<T> source,
        Func<T, Task<Result<TResult>>> selector)
    {
        var tasks = source.Select(selector);
        var results = await Task.WhenAll(tasks);
        return results.Combine();
    }

    /// <summary>
    /// Nullable&lt;T&gt; を Result&lt;T&gt; に変換します
    /// </summary>
    public static Result<T> ToResult<T>(this T? nullable, string errorMessage = "Value is null") where T : struct
    {
        return nullable.HasValue
            ? Result<T>.Success(nullable.Value)
            : Result<T>.Failure(errorMessage);
    }

    /// <summary>
    /// 参照型の null チェックを Result&lt;T&gt; に変換します
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, string errorMessage = "Value is null") where T : class
    {
        return value != null
            ? Result<T>.Success(value)
            : Result<T>.Failure(errorMessage);
    }

    /// <summary>
    /// Result を Result&lt;T&gt; に変換します（値を指定）
    /// </summary>
    public static Result<T> WithValue<T>(this Result result, T value)
    {
        return result.IsSuccess
            ? Result<T>.Success(value)
            : Result<T>.Failure(result.ErrorMessage!, result.Exception!, result.ErrorCode!);
    }

    /// <summary>
    /// Result&lt;T&gt; を Result に変換します（値を破棄）
    /// </summary>
    public static Result ToResult<T>(this Result<T> result)
    {
        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.ErrorMessage!, result.Exception!, result.ErrorCode!);
    }

    /// <summary>
    /// Result&lt;T&gt; の値を検証します
    /// </summary>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, string errorMessage)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? result
            : Result<T>.Failure(errorMessage);
    }

    /// <summary>
    /// Result&lt;T&gt; の値を非同期に検証します
    /// </summary>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<bool>> predicate,
        string errorMessage)
    {
        if (result.IsFailure)
            return result;

        var isValid = await predicate(result.Value);
        return isValid
            ? result
            : Result<T>.Failure(errorMessage);
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して Ensure を実行します
    /// </summary>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> predicate,
        string errorMessage)
    {
        var result = await resultTask;
        return result.Ensure(predicate, errorMessage);
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して Map を実行します
    /// </summary>
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, TNew> mapper)
    {
        var result = await resultTask;
        return result.Map(mapper);
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して非同期 Map を実行します
    /// </summary>
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, Task<TNew>> mapper)
    {
        var result = await resultTask;
        return await result.MapAsync(mapper);
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して Bind を実行します
    /// </summary>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, Result<TNew>> binder)
    {
        var result = await resultTask;
        return result.Bind(binder);
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して非同期 Bind を実行します
    /// </summary>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, Task<Result<TNew>>> binder)
    {
        var result = await resultTask;
        return await result.BindAsync(binder);
    }
}

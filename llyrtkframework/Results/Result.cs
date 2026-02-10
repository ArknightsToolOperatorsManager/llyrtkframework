namespace llyrtkframework.Results;

/// <summary>
/// 操作結果を表す基底クラス（値を返さない操作用）
/// </summary>
public class Result
{
    /// <summary>
    /// 操作が成功したかどうか
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 操作が失敗したかどうか
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// 発生した例外（存在する場合）
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// エラーコード（オプション）
    /// </summary>
    public string? ErrorCode { get; }

    protected Result(bool isSuccess, string? errorMessage, Exception? exception = null, string? errorCode = null)
    {
        if (isSuccess && !string.IsNullOrEmpty(errorMessage))
            throw new InvalidOperationException("Success result cannot have an error message.");

        if (!isSuccess && string.IsNullOrEmpty(errorMessage))
            throw new InvalidOperationException("Failure result must have an error message.");

        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// 成功結果を作成します
    /// </summary>
    public static Result Success()
    {
        return new Result(true, null);
    }

    /// <summary>
    /// 失敗結果を作成します
    /// </summary>
    /// <param name="errorMessage">エラーメッセージ</param>
    public static Result Failure(string errorMessage)
    {
        return new Result(false, errorMessage);
    }

    /// <summary>
    /// 失敗結果を作成します（例外付き）
    /// </summary>
    /// <param name="errorMessage">エラーメッセージ</param>
    /// <param name="exception">例外</param>
    public static Result Failure(string errorMessage, Exception exception)
    {
        return new Result(false, errorMessage, exception);
    }

    /// <summary>
    /// 失敗結果を作成します（エラーコード付き）
    /// </summary>
    /// <param name="errorMessage">エラーメッセージ</param>
    /// <param name="errorCode">エラーコード</param>
    public static Result Failure(string errorMessage, string errorCode)
    {
        return new Result(false, errorMessage, null, errorCode);
    }

    /// <summary>
    /// 失敗結果を作成します（例外とエラーコード付き）
    /// </summary>
    public static Result Failure(string errorMessage, Exception exception, string errorCode)
    {
        return new Result(false, errorMessage, exception, errorCode);
    }

    /// <summary>
    /// 例外から失敗結果を作成します
    /// </summary>
    public static Result FromException(Exception exception, string? customMessage = null)
    {
        var message = customMessage ?? exception.Message;
        return new Result(false, message, exception);
    }

    /// <summary>
    /// 条件によって成功または失敗の結果を返します
    /// </summary>
    public static Result SuccessIf(bool condition, string errorMessage)
    {
        return condition ? Success() : Failure(errorMessage);
    }

    /// <summary>
    /// 条件によって失敗または成功の結果を返します
    /// </summary>
    public static Result FailureIf(bool condition, string errorMessage)
    {
        return condition ? Failure(errorMessage) : Success();
    }

    /// <summary>
    /// 複数の結果を結合します（すべて成功の場合のみ成功）
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
                return result;
        }
        return Success();
    }

    /// <summary>
    /// 複数の結果を結合します（すべて成功の場合のみ成功、エラーメッセージをすべて含む）
    /// </summary>
    public static Result CombineAll(params Result[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Count == 0)
            return Success();

        var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
        return Failure(errorMessages);
    }

    public override string ToString()
    {
        return IsSuccess
            ? "Success"
            : $"Failure: {ErrorMessage}" + (ErrorCode != null ? $" (Code: {ErrorCode})" : "");
    }
}

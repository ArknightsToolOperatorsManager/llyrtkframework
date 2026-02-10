using llyrtkframework.Results;

namespace llyrtkframework.Guard;

/// <summary>
/// 防御的プログラミングのためのガード句ヘルパー
/// </summary>
public static class Guard
{
    /// <summary>
    /// 値が null でないことを検証します
    /// </summary>
    /// <param name="value">検証する値</param>
    /// <param name="paramName">パラメータ名</param>
    /// <returns>検証結果</returns>
    public static Result AgainstNull(object? value, string paramName)
    {
        return value != null
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be null");
    }

    /// <summary>
    /// 値が null でないことを検証し、値を返します
    /// </summary>
    public static Result<T> AgainstNull<T>(T? value, string paramName) where T : class
    {
        return value != null
            ? Result<T>.Success(value)
            : Result<T>.Failure($"{paramName} cannot be null");
    }

    /// <summary>
    /// 文字列が null または空でないことを検証します
    /// </summary>
    public static Result AgainstNullOrEmpty(string? value, string paramName)
    {
        return !string.IsNullOrEmpty(value)
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be null or empty");
    }

    /// <summary>
    /// 文字列が null または空でないことを検証し、値を返します
    /// </summary>
    public static Result<string> AgainstNullOrEmptyWithValue(string? value, string paramName)
    {
        return !string.IsNullOrEmpty(value)
            ? Result<string>.Success(value)
            : Result<string>.Failure($"{paramName} cannot be null or empty");
    }

    /// <summary>
    /// 文字列が null、空、または空白文字のみでないことを検証します
    /// </summary>
    public static Result AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be null, empty, or whitespace");
    }

    /// <summary>
    /// 文字列が null、空、または空白文字のみでないことを検証し、値を返します
    /// </summary>
    public static Result<string> AgainstNullOrWhiteSpaceWithValue(string? value, string paramName)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? Result<string>.Success(value)
            : Result<string>.Failure($"{paramName} cannot be null, empty, or whitespace");
    }

    /// <summary>
    /// 数値が負でないことを検証します
    /// </summary>
    public static Result AgainstNegative(int value, string paramName)
    {
        return value >= 0
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be negative. Value: {value}");
    }

    /// <summary>
    /// 数値が負でないことを検証します
    /// </summary>
    public static Result AgainstNegative(long value, string paramName)
    {
        return value >= 0
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be negative. Value: {value}");
    }

    /// <summary>
    /// 数値が負でないことを検証します
    /// </summary>
    public static Result AgainstNegative(decimal value, string paramName)
    {
        return value >= 0
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be negative. Value: {value}");
    }

    /// <summary>
    /// 数値が負でないことを検証します
    /// </summary>
    public static Result AgainstNegative(double value, string paramName)
    {
        return value >= 0
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be negative. Value: {value}");
    }

    /// <summary>
    /// 数値が正であることを検証します
    /// </summary>
    public static Result AgainstNegativeOrZero(int value, string paramName)
    {
        return value > 0
            ? Result.Success()
            : Result.Failure($"{paramName} must be positive. Value: {value}");
    }

    /// <summary>
    /// 数値が正であることを検証します
    /// </summary>
    public static Result AgainstNegativeOrZero(long value, string paramName)
    {
        return value > 0
            ? Result.Success()
            : Result.Failure($"{paramName} must be positive. Value: {value}");
    }

    /// <summary>
    /// 数値が正であることを検証します
    /// </summary>
    public static Result AgainstNegativeOrZero(decimal value, string paramName)
    {
        return value > 0
            ? Result.Success()
            : Result.Failure($"{paramName} must be positive. Value: {value}");
    }

    /// <summary>
    /// 数値が正であることを検証します
    /// </summary>
    public static Result AgainstNegativeOrZero(double value, string paramName)
    {
        return value > 0
            ? Result.Success()
            : Result.Failure($"{paramName} must be positive. Value: {value}");
    }

    /// <summary>
    /// 数値が範囲内にあることを検証します
    /// </summary>
    public static Result AgainstOutOfRange(int value, int min, int max, string paramName)
    {
        return value >= min && value <= max
            ? Result.Success()
            : Result.Failure($"{paramName} must be between {min} and {max}. Value: {value}");
    }

    /// <summary>
    /// 数値が範囲内にあることを検証します
    /// </summary>
    public static Result AgainstOutOfRange(long value, long min, long max, string paramName)
    {
        return value >= min && value <= max
            ? Result.Success()
            : Result.Failure($"{paramName} must be between {min} and {max}. Value: {value}");
    }

    /// <summary>
    /// 数値が範囲内にあることを検証します
    /// </summary>
    public static Result AgainstOutOfRange(decimal value, decimal min, decimal max, string paramName)
    {
        return value >= min && value <= max
            ? Result.Success()
            : Result.Failure($"{paramName} must be between {min} and {max}. Value: {value}");
    }

    /// <summary>
    /// 数値が範囲内にあることを検証します
    /// </summary>
    public static Result AgainstOutOfRange(double value, double min, double max, string paramName)
    {
        return value >= min && value <= max
            ? Result.Success()
            : Result.Failure($"{paramName} must be between {min} and {max}. Value: {value}");
    }

    /// <summary>
    /// コレクションが null または空でないことを検証します
    /// </summary>
    public static Result AgainstNullOrEmpty<T>(IEnumerable<T>? collection, string paramName)
    {
        return collection != null && collection.Any()
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be null or empty");
    }

    /// <summary>
    /// コレクションが null または空でないことを検証し、値を返します
    /// </summary>
    public static Result<IEnumerable<T>> AgainstNullOrEmptyWithValue<T>(IEnumerable<T>? collection, string paramName)
    {
        return collection != null && collection.Any()
            ? Result<IEnumerable<T>>.Success(collection)
            : Result<IEnumerable<T>>.Failure($"{paramName} cannot be null or empty");
    }

    /// <summary>
    /// GUID が空でないことを検証します
    /// </summary>
    public static Result AgainstEmptyGuid(Guid value, string paramName)
    {
        return value != Guid.Empty
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be empty GUID");
    }

    /// <summary>
    /// GUID が空でないことを検証し、値を返します
    /// </summary>
    public static Result<Guid> AgainstEmptyGuidWithValue(Guid value, string paramName)
    {
        return value != Guid.Empty
            ? Result<Guid>.Success(value)
            : Result<Guid>.Failure($"{paramName} cannot be empty GUID");
    }

    /// <summary>
    /// 日付が過去でないことを検証します
    /// </summary>
    public static Result AgainstPastDate(DateTime value, string paramName)
    {
        return value >= DateTime.UtcNow
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be in the past. Value: {value:u}");
    }

    /// <summary>
    /// 日付が未来でないことを検証します
    /// </summary>
    public static Result AgainstFutureDate(DateTime value, string paramName)
    {
        return value <= DateTime.UtcNow
            ? Result.Success()
            : Result.Failure($"{paramName} cannot be in the future. Value: {value:u}");
    }

    /// <summary>
    /// 文字列の長さが最大値を超えないことを検証します
    /// </summary>
    public static Result AgainstMaxLength(string? value, int maxLength, string paramName)
    {
        if (value == null)
            return Result.Success();

        return value.Length <= maxLength
            ? Result.Success()
            : Result.Failure($"{paramName} cannot exceed {maxLength} characters. Length: {value.Length}");
    }

    /// <summary>
    /// 文字列の長さが最小値以上であることを検証します
    /// </summary>
    public static Result AgainstMinLength(string? value, int minLength, string paramName)
    {
        if (value == null)
            return Result.Failure($"{paramName} cannot be null");

        return value.Length >= minLength
            ? Result.Success()
            : Result.Failure($"{paramName} must be at least {minLength} characters. Length: {value.Length}");
    }

    /// <summary>
    /// 文字列の長さが範囲内にあることを検証します
    /// </summary>
    public static Result AgainstLengthOutOfRange(string? value, int minLength, int maxLength, string paramName)
    {
        if (value == null)
            return Result.Failure($"{paramName} cannot be null");

        return value.Length >= minLength && value.Length <= maxLength
            ? Result.Success()
            : Result.Failure($"{paramName} length must be between {minLength} and {maxLength}. Length: {value.Length}");
    }

    /// <summary>
    /// 条件が真であることを検証します
    /// </summary>
    public static Result AgainstCondition(bool condition, string errorMessage)
    {
        return condition
            ? Result.Success()
            : Result.Failure(errorMessage);
    }

    /// <summary>
    /// 複数のガード句を結合して検証します
    /// </summary>
    public static Result Combine(params Result[] guards)
    {
        return Result.Combine(guards);
    }
}

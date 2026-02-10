using FluentValidation;
using llyrtkframework.Results;

namespace llyrtkframework.Validation;

/// <summary>
/// FluentValidation と Result パターンを統合する拡張メソッド
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// モデルを検証し、Result&lt;T&gt; を返します
    /// </summary>
    /// <typeparam name="T">検証対象の型</typeparam>
    /// <param name="model">検証対象のモデル</param>
    /// <param name="validator">バリデーター</param>
    /// <returns>検証結果</returns>
    public static Result<T> ValidateAndReturn<T>(this T model, IValidator<T> validator)
    {
        var validationResult = validator.Validate(model);

        if (validationResult.IsValid)
            return Result<T>.Success(model);

        var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Result<T>.Failure(errorMessages);
    }

    /// <summary>
    /// モデルを検証し、Result を返します（値を破棄）
    /// </summary>
    public static Result Validate<T>(this T model, IValidator<T> validator)
    {
        var validationResult = validator.Validate(model);

        if (validationResult.IsValid)
            return Result.Success();

        var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Result.Failure(errorMessages);
    }

    /// <summary>
    /// 非同期でモデルを検証し、Result&lt;T&gt; を返します
    /// </summary>
    public static async Task<Result<T>> ValidateAndReturnAsync<T>(
        this T model,
        IValidator<T> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(model, cancellationToken);

        if (validationResult.IsValid)
            return Result<T>.Success(model);

        var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Result<T>.Failure(errorMessages);
    }

    /// <summary>
    /// 非同期でモデルを検証し、Result を返します
    /// </summary>
    public static async Task<Result> ValidateAsync<T>(
        this T model,
        IValidator<T> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(model, cancellationToken);

        if (validationResult.IsValid)
            return Result.Success();

        var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Result.Failure(errorMessages);
    }

    /// <summary>
    /// 検証結果を Result に変換します
    /// </summary>
    public static Result ToResult(this FluentValidation.Results.ValidationResult validationResult)
    {
        if (validationResult.IsValid)
            return Result.Success();

        var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Result.Failure(errorMessages);
    }

    /// <summary>
    /// 検証結果を Result&lt;T&gt; に変換します
    /// </summary>
    public static Result<T> ToResult<T>(
        this FluentValidation.Results.ValidationResult validationResult,
        T model)
    {
        if (validationResult.IsValid)
            return Result<T>.Success(model);

        var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Result<T>.Failure(errorMessages);
    }

    /// <summary>
    /// エラーメッセージを辞書形式で取得します（プロパティ名 => エラーメッセージ）
    /// </summary>
    public static Dictionary<string, List<string>> GetErrorDictionary(
        this FluentValidation.Results.ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToList()
            );
    }

    /// <summary>
    /// Result に対して検証を実行します（Railway Oriented Programming）
    /// </summary>
    public static Result<T> ValidateResult<T>(
        this Result<T> result,
        IValidator<T> validator)
    {
        if (result.IsFailure)
            return result;

        return result.Value.ValidateAndReturn(validator);
    }

    /// <summary>
    /// Result に対して非同期検証を実行します
    /// </summary>
    public static async Task<Result<T>> ValidateResultAsync<T>(
        this Result<T> result,
        IValidator<T> validator,
        CancellationToken cancellationToken = default)
    {
        if (result.IsFailure)
            return result;

        return await result.Value.ValidateAndReturnAsync(validator, cancellationToken);
    }

    /// <summary>
    /// Task&lt;Result&lt;T&gt;&gt; に対して検証を実行します
    /// </summary>
    public static async Task<Result<T>> ValidateResultAsync<T>(
        this Task<Result<T>> resultTask,
        IValidator<T> validator,
        CancellationToken cancellationToken = default)
    {
        var result = await resultTask;
        return await result.ValidateResultAsync(validator, cancellationToken);
    }
}

using FluentValidation;

namespace llyrtkframework.Validation;

/// <summary>
/// 共通のバリデーションルールを提供する基底バリデータークラス
/// </summary>
/// <typeparam name="T">検証対象の型</typeparam>
public abstract class AbstractValidatorBase<T> : AbstractValidator<T>
{
    /// <summary>
    /// Guid が空でないことを検証します
    /// </summary>
    protected IRuleBuilderOptions<T, Guid> NotEmptyGuid<TProp>(IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
            .NotEqual(Guid.Empty)
            .WithMessage("{PropertyName} cannot be empty GUID");
    }

    /// <summary>
    /// Nullable Guid が空でないことを検証します
    /// </summary>
    protected IRuleBuilderOptions<T, Guid?> NotEmptyGuid<TProp>(IRuleBuilder<T, Guid?> ruleBuilder)
    {
        return ruleBuilder
            .NotNull()
            .NotEqual(Guid.Empty)
            .WithMessage("{PropertyName} cannot be empty GUID");
    }

    /// <summary>
    /// 文字列が URL 形式であることを検証します
    /// </summary>
    protected IRuleBuilderOptions<T, string> IsValidUrl<TProp>(IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("{PropertyName} must be a valid URL");
    }

    /// <summary>
    /// 電話番号の形式を検証します（簡易版）
    /// </summary>
    protected IRuleBuilderOptions<T, string> IsPhoneNumber<TProp>(IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches(@"^[\d\-\+\(\)\s]+$")
            .WithMessage("{PropertyName} must be a valid phone number");
    }

    /// <summary>
    /// 郵便番号の形式を検証します（日本の郵便番号）
    /// </summary>
    protected IRuleBuilderOptions<T, string> IsJapanesePostalCode<TProp>(IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches(@"^\d{3}-?\d{4}$")
            .WithMessage("{PropertyName} must be a valid Japanese postal code (e.g., 123-4567)");
    }

    /// <summary>
    /// 日付が過去でないことを検証します
    /// </summary>
    protected IRuleBuilderOptions<T, DateTime> NotInPast<TProp>(IRuleBuilder<T, DateTime> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("{PropertyName} cannot be in the past");
    }

    /// <summary>
    /// 日付が未来でないことを検証します
    /// </summary>
    protected IRuleBuilderOptions<T, DateTime> NotInFuture<TProp>(IRuleBuilder<T, DateTime> ruleBuilder)
    {
        return ruleBuilder
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("{PropertyName} cannot be in the future");
    }

    /// <summary>
    /// コレクションの最大要素数を検証します
    /// </summary>
    protected IRuleBuilderOptions<T, IEnumerable<TElement>> MaximumCount<TElement>(
        IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder,
        int max)
    {
        return ruleBuilder
            .Must(collection => collection.Count() <= max)
            .WithMessage($"{{PropertyName}} cannot have more than {max} items");
    }

    /// <summary>
    /// コレクションの最小要素数を検証します
    /// </summary>
    protected IRuleBuilderOptions<T, IEnumerable<TElement>> MinimumCount<TElement>(
        IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder,
        int min)
    {
        return ruleBuilder
            .Must(collection => collection.Count() >= min)
            .WithMessage($"{{PropertyName}} must have at least {min} items");
    }
}

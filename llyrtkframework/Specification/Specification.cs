using System.Linq.Expressions;

namespace llyrtkframework.Specification;

/// <summary>
/// 仕様パターンの基底クラス
/// </summary>
/// <typeparam name="T">評価対象の型</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
    /// <summary>
    /// 仕様を Expression として返します
    /// </summary>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// エンティティが仕様を満たすかどうかを判定します
    /// </summary>
    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }

    /// <summary>
    /// AND 演算子（両方の仕様を満たす）
    /// </summary>
    public Specification<T> And(Specification<T> other)
    {
        return new AndSpecification<T>(this, other);
    }

    /// <summary>
    /// OR 演算子（いずれかの仕様を満たす）
    /// </summary>
    public Specification<T> Or(Specification<T> other)
    {
        return new OrSpecification<T>(this, other);
    }

    /// <summary>
    /// NOT 演算子（仕様を満たさない）
    /// </summary>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }

    /// <summary>
    /// 暗黙的に Expression&lt;Func&lt;T, bool&gt;&gt; に変換します
    /// </summary>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        return specification.ToExpression();
    }

    /// <summary>
    /// AND 演算子のオーバーロード
    /// </summary>
    public static Specification<T> operator &(Specification<T> left, Specification<T> right)
    {
        return new AndSpecification<T>(left, right);
    }

    /// <summary>
    /// OR 演算子のオーバーロード
    /// </summary>
    public static Specification<T> operator |(Specification<T> left, Specification<T> right)
    {
        return new OrSpecification<T>(left, right);
    }

    /// <summary>
    /// NOT 演算子のオーバーロード
    /// </summary>
    public static Specification<T> operator !(Specification<T> specification)
    {
        return new NotSpecification<T>(specification);
    }
}

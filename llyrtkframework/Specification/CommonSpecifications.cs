using System.Linq.Expressions;

namespace llyrtkframework.Specification;

/// <summary>
/// すべてのエンティティを満たす仕様
/// </summary>
public class AllSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return x => true;
    }
}

/// <summary>
/// どのエンティティも満たさない仕様
/// </summary>
public class NoneSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return x => false;
    }
}

/// <summary>
/// ラムダ式から仕様を作成するクラス
/// </summary>
public class ExpressionSpecification<T> : Specification<T>
{
    private readonly Expression<Func<T, bool>> _expression;

    public ExpressionSpecification(Expression<Func<T, bool>> expression)
    {
        _expression = expression;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        return _expression;
    }
}

/// <summary>
/// 仕様パターンのヘルパークラス
/// </summary>
public static class SpecificationBuilder
{
    /// <summary>
    /// ラムダ式から仕様を作成します
    /// </summary>
    public static Specification<T> Create<T>(Expression<Func<T, bool>> expression)
    {
        return new ExpressionSpecification<T>(expression);
    }

    /// <summary>
    /// すべてのエンティティを満たす仕様を作成します
    /// </summary>
    public static Specification<T> All<T>()
    {
        return new AllSpecification<T>();
    }

    /// <summary>
    /// どのエンティティも満たさない仕様を作成します
    /// </summary>
    public static Specification<T> None<T>()
    {
        return new NoneSpecification<T>();
    }
}

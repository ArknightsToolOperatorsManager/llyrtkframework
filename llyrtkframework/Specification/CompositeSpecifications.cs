using System.Linq.Expressions;

namespace llyrtkframework.Specification;

/// <summary>
/// AND 仕様（両方の仕様を満たす）
/// </summary>
internal class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");

        var leftBody = new ParameterReplacer(leftExpression.Parameters[0], parameter)
            .Visit(leftExpression.Body);
        var rightBody = new ParameterReplacer(rightExpression.Parameters[0], parameter)
            .Visit(rightExpression.Body);

        var andExpression = Expression.AndAlso(leftBody!, rightBody!);

        return Expression.Lambda<Func<T, bool>>(andExpression, parameter);
    }
}

/// <summary>
/// OR 仕様（いずれかの仕様を満たす）
/// </summary>
internal class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");

        var leftBody = new ParameterReplacer(leftExpression.Parameters[0], parameter)
            .Visit(leftExpression.Body);
        var rightBody = new ParameterReplacer(rightExpression.Parameters[0], parameter)
            .Visit(rightExpression.Body);

        var orExpression = Expression.OrElse(leftBody!, rightBody!);

        return Expression.Lambda<Func<T, bool>>(orExpression, parameter);
    }
}

/// <summary>
/// NOT 仕様（仕様を満たさない）
/// </summary>
internal class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _specification;

    public NotSpecification(Specification<T> specification)
    {
        _specification = specification;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _specification.ToExpression();
        var parameter = expression.Parameters[0];
        var notExpression = Expression.Not(expression.Body);

        return Expression.Lambda<Func<T, bool>>(notExpression, parameter);
    }
}

/// <summary>
/// Expression のパラメータを置き換えるヘルパークラス
/// </summary>
internal class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _oldParameter;
    private readonly ParameterExpression _newParameter;

    public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        _oldParameter = oldParameter;
        _newParameter = newParameter;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}

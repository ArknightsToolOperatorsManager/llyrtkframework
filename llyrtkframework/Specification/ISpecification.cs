using System.Linq.Expressions;

namespace llyrtkframework.Specification;

/// <summary>
/// 仕様パターンのインターフェース
/// </summary>
/// <typeparam name="T">評価対象の型</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    /// エンティティが仕様を満たすかどうかを判定します
    /// </summary>
    /// <param name="entity">評価対象のエンティティ</param>
    /// <returns>仕様を満たす場合は true</returns>
    bool IsSatisfiedBy(T entity);

    /// <summary>
    /// 仕様を Expression として返します（LINQ クエリで使用可能）
    /// </summary>
    /// <returns>評価式</returns>
    Expression<Func<T, bool>> ToExpression();
}

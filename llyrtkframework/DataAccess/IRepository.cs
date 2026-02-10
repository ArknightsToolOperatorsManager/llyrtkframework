using llyrtkframework.Results;
using llyrtkframework.Specification;
using System.Linq.Expressions;

namespace llyrtkframework.DataAccess;

/// <summary>
/// リポジトリパターンのインターフェース
/// </summary>
/// <typeparam name="T">エンティティの型</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// エンティティを追加します
    /// </summary>
    Task<Result<T>> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// エンティティを更新します
    /// </summary>
    Task<Result<T>> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// エンティティを削除します
    /// </summary>
    Task<Result> DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// IDでエンティティを取得します
    /// </summary>
    Task<Result<T>> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// すべてのエンティティを取得します
    /// </summary>
    Task<Result<IEnumerable<T>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 条件に一致するエンティティを検索します
    /// </summary>
    Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Specificationパターンで検索します
    /// </summary>
    Task<Result<IEnumerable<T>>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// エンティティが存在するかチェックします
    /// </summary>
    Task<Result<bool>> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// エンティティの数を取得します
    /// </summary>
    Task<Result<int>> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 条件に一致するエンティティの数を取得します
    /// </summary>
    Task<Result<int>> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}

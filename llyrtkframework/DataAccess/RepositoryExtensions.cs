using llyrtkframework.Results;
using System.Linq.Expressions;

namespace llyrtkframework.DataAccess;

/// <summary>
/// Repository関連の拡張メソッド
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// 最初のエンティティを取得します
    /// </summary>
    public static async Task<Result<T>> FirstOrDefaultAsync<T>(
        this IRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) where T : class
    {
        var result = await repository.FindAsync(predicate, cancellationToken);

        if (result.IsFailure)
            return Result<T>.Failure(result.ErrorMessage ?? "Unknown error");

        var first = result.Value!.FirstOrDefault();

        return first != null
            ? Result<T>.Success(first)
            : Result<T>.Failure("Entity not found");
    }

    /// <summary>
    /// 複数のエンティティを一括追加します
    /// </summary>
    public static async Task<Result<IEnumerable<T>>> AddRangeAsync<T>(
        this IRepository<T> repository,
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default) where T : class
    {
        var results = new List<T>();

        foreach (var entity in entities)
        {
            var result = await repository.AddAsync(entity, cancellationToken);
            if (result.IsFailure)
                return Result<IEnumerable<T>>.Failure(result.ErrorMessage ?? "Unknown error");

            results.Add(result.Value!);
        }

        return Result<IEnumerable<T>>.Success(results);
    }

    /// <summary>
    /// 複数のエンティティを一括削除します
    /// </summary>
    public static async Task<Result> DeleteRangeAsync<T>(
        this IRepository<T> repository,
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default) where T : class
    {
        foreach (var entity in entities)
        {
            var result = await repository.DeleteAsync(entity, cancellationToken);
            if (result.IsFailure)
                return result;
        }

        return Result.Success();
    }

    /// <summary>
    /// ページネーション付きで取得します
    /// </summary>
    public static async Task<Result<PagedResult<T>>> GetPagedAsync<T>(
        this IRepository<T> repository,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) where T : class
    {
        var allResult = await repository.GetAllAsync(cancellationToken);

        if (allResult.IsFailure)
            return Result<PagedResult<T>>.Failure(allResult.ErrorMessage ?? "Unknown error");

        var items = allResult.Value!
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalCount = allResult.Value!.Count();

        return Result<PagedResult<T>>.Success(new PagedResult<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}

/// <summary>
/// ページング結果
/// </summary>
/// <typeparam name="T">エンティティの型</typeparam>
public class PagedResult<T>
{
    public required IEnumerable<T> Items { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required int TotalPages { get; init; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

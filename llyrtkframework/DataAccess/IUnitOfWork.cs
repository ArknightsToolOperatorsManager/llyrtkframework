using llyrtkframework.Results;

namespace llyrtkframework.DataAccess;

/// <summary>
/// Unit of Workパターンのインターフェース
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// リポジトリを取得します
    /// </summary>
    IRepository<T> GetRepository<T>() where T : class;

    /// <summary>
    /// 変更をコミットします
    /// </summary>
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 変更をロールバックします
    /// </summary>
    Task<Result> RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// トランザクションを開始します
    /// </summary>
    Task<Result> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

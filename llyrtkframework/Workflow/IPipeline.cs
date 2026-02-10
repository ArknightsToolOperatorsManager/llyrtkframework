using llyrtkframework.Results;

namespace llyrtkframework.Workflow;

/// <summary>
/// パイプラインのインターフェース
/// </summary>
/// <typeparam name="TInput">入力の型</typeparam>
/// <typeparam name="TOutput">出力の型</typeparam>
public interface IPipeline<TInput, TOutput>
{
    /// <summary>
    /// パイプラインを実行します
    /// </summary>
    Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// パイプラインステップのインターフェース
/// </summary>
/// <typeparam name="TInput">入力の型</typeparam>
/// <typeparam name="TOutput">出力の型</typeparam>
public interface IPipelineStep<TInput, TOutput>
{
    /// <summary>
    /// ステップを実行します
    /// </summary>
    Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// ステップ名
    /// </summary>
    string Name { get; }
}

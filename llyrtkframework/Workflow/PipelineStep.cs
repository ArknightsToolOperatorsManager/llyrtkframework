using llyrtkframework.Results;

namespace llyrtkframework.Workflow;

/// <summary>
/// パイプラインステップの基底クラス
/// </summary>
/// <typeparam name="TInput">入力の型</typeparam>
/// <typeparam name="TOutput">出力の型</typeparam>
public abstract class PipelineStep<TInput, TOutput> : IPipelineStep<TInput, TOutput>
{
    public abstract string Name { get; }

    public abstract Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// デリゲートベースのパイプラインステップ
/// </summary>
/// <typeparam name="TInput">入力の型</typeparam>
/// <typeparam name="TOutput">出力の型</typeparam>
public class DelegatePipelineStep<TInput, TOutput> : IPipelineStep<TInput, TOutput>
{
    private readonly Func<TInput, CancellationToken, Task<Result<TOutput>>> _executeFunc;

    public string Name { get; }

    public DelegatePipelineStep(
        string name,
        Func<TInput, CancellationToken, Task<Result<TOutput>>> executeFunc)
    {
        Name = name;
        _executeFunc = executeFunc;
    }

    public Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
    {
        return _executeFunc(input, cancellationToken);
    }
}

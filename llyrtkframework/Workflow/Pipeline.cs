using llyrtkframework.Results;
using Microsoft.Extensions.Logging;

namespace llyrtkframework.Workflow;

/// <summary>
/// パイプラインの実装
/// </summary>
/// <typeparam name="TInput">入力の型</typeparam>
/// <typeparam name="TOutput">出力の型</typeparam>
public class Pipeline<TInput, TOutput> : IPipeline<TInput, TOutput>
{
    private readonly List<Func<object, CancellationToken, Task<Result<object>>>> _steps = new();
    private readonly ILogger? _logger;

    public Pipeline(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// パイプラインにステップを追加します
    /// </summary>
    public Pipeline<TInput, TOutput> AddStep<TStepInput, TStepOutput>(
        IPipelineStep<TStepInput, TStepOutput> step)
    {
        _steps.Add(async (input, ct) =>
        {
            _logger?.LogDebug("Executing pipeline step: {StepName}", step.Name);

            if (input is not TStepInput typedInput)
            {
                return Result<object>.Failure($"Invalid input type for step {step.Name}");
            }

            var result = await step.ExecuteAsync(typedInput, ct);

            if (result.IsFailure)
            {
                _logger?.LogWarning("Pipeline step {StepName} failed: {Error}", step.Name, result.ErrorMessage);
                return Result<object>.Failure(result.ErrorMessage ?? "Unknown error");
            }

            return Result<object>.Success(result.Value!);
        });

        return this;
    }

    /// <summary>
    /// パイプラインにステップを追加します（関数版）
    /// </summary>
    public Pipeline<TInput, TOutput> AddStep<TStepInput, TStepOutput>(
        string stepName,
        Func<TStepInput, CancellationToken, Task<Result<TStepOutput>>> stepFunc)
    {
        _steps.Add(async (input, ct) =>
        {
            _logger?.LogDebug("Executing pipeline step: {StepName}", stepName);

            if (input is not TStepInput typedInput)
            {
                return Result<object>.Failure($"Invalid input type for step {stepName}");
            }

            var result = await stepFunc(typedInput, ct);

            if (result.IsFailure)
            {
                _logger?.LogWarning("Pipeline step {StepName} failed: {Error}", stepName, result.ErrorMessage);
                return Result<object>.Failure(result.ErrorMessage ?? "Unknown error");
            }

            return Result<object>.Success(result.Value!);
        });

        return this;
    }

    public async Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Starting pipeline execution with {StepCount} steps", _steps.Count);

            object current = input!;

            foreach (var step in _steps)
            {
                var result = await step(current, cancellationToken);

                if (result.IsFailure)
                {
                    _logger?.LogError("Pipeline execution failed: {Error}", result.ErrorMessage);
                    return Result<TOutput>.Failure(result.ErrorMessage ?? "Unknown error");
                }

                current = result.Value!;
            }

            if (current is not TOutput output)
            {
                return Result<TOutput>.Failure("Pipeline output type mismatch");
            }

            _logger?.LogInformation("Pipeline execution completed successfully");
            return Result<TOutput>.Success(output);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pipeline execution error");
            return Result<TOutput>.FromException(ex, "Pipeline execution error");
        }
    }
}

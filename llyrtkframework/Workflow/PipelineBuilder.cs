using Microsoft.Extensions.Logging;

namespace llyrtkframework.Workflow;

/// <summary>
/// パイプラインビルダー（Fluent API）
/// </summary>
public class PipelineBuilder
{
    private readonly ILogger? _logger;

    public PipelineBuilder(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 新しいパイプラインを作成します
    /// </summary>
    public static PipelineBuilder Create(ILogger? logger = null)
    {
        return new PipelineBuilder(logger);
    }

    /// <summary>
    /// パイプラインを構築します
    /// </summary>
    public Pipeline<TInput, TOutput> Build<TInput, TOutput>()
    {
        return new Pipeline<TInput, TOutput>(_logger);
    }
}

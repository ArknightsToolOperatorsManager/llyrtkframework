namespace llyrtkframework.FileManagement.Core;

/// <summary>
/// ロールバックオプション
/// </summary>
public class RollbackOptions
{
    /// <summary>
    /// 最大リトライ回数（-1で全バックアップを試行）
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 各リトライ間の遅延
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 全失敗時に例外をスローするか（falseの場合はResult.Failureを返す）
    /// </summary>
    public bool ThrowOnAllFailed { get; set; } = false;
}

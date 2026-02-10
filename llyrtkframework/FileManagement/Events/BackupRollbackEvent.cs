namespace llyrtkframework.FileManagement.Events;

/// <summary>
/// バックアップロールバックイベント
/// </summary>
public class BackupRollbackEvent
{
    /// <summary>対象ファイルパス</summary>
    public required string FilePath { get; init; }

    /// <summary>試行したバックアップパスのリスト</summary>
    public required List<string> TriedBackupPaths { get; init; }

    /// <summary>成功したバックアップパス（全失敗時はnull）</summary>
    public string? SuccessfulBackupPath { get; init; }

    /// <summary>各失敗の理由</summary>
    public required List<string> FailureReasons { get; init; }

    /// <summary>全バックアップが失敗したか</summary>
    public bool IsFullFailure => SuccessfulBackupPath == null;

    /// <summary>発生日時</summary>
    public DateTime OccurredAt { get; init; } = DateTime.Now;
}

namespace llyrtkframework.FileManagement.Backup;

/// <summary>
/// バックアップの設定オプション
/// </summary>
public class BackupOptions
{
    /// <summary>保持するバックアップの最大数</summary>
    public int MaxBackupCount { get; set; } = 10;

    /// <summary>バックアップの保存期間</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>バックアップディレクトリ（nullの場合は元ファイルと同じ場所に .backup サブフォルダ作成）</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>バックアップファイル名のパターン（{filename}と{timestamp}を使用可能）</summary>
    public string BackupFilePattern { get; set; } = "{filename}_{timestamp}.bak";
}

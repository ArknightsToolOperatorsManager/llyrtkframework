using llyrtkframework.FileManagement.Backup;
using llyrtkframework.FileManagement.GitHub;
using llyrtkframework.FileManagement.Triggers;

namespace llyrtkframework.FileManagement.Core;

/// <summary>
/// ファイル管理のオプション
/// </summary>
public class FileOptions
{
    /// <summary>ファイルパス（相対または絶対）</summary>
    public required string FilePath { get; set; }

    /// <summary>バックアップトリガーのリスト</summary>
    public List<BackupTrigger> BackupTriggers { get; set; } = new();

    /// <summary>バックアップ設定</summary>
    public BackupOptions Backup { get; set; } = new();

    /// <summary>GitHub同期オプション（null = GitHub同期無効）</summary>
    public GitHubFileOptions? GitHub { get; set; }
}

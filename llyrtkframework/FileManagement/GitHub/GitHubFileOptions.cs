namespace llyrtkframework.FileManagement.GitHub;

/// <summary>
/// GitHubファイル連携の設定オプション
/// </summary>
public class GitHubFileOptions
{
    /// <summary>リポジトリオーナー（ユーザー名または組織名）</summary>
    public required string Owner { get; set; }

    /// <summary>リポジトリ名</summary>
    public required string Repository { get; set; }

    /// <summary>ブランチ名</summary>
    public string Branch { get; set; } = "main";

    /// <summary>リポジトリ内のファイルパス</summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// Personal Access Token（プライベートリポジトリ用）
    /// null = パブリックリポジトリとしてraw.githubusercontent.comから直接ダウンロード（レート制限なし）
    /// 値あり = GitHub API経由でダウンロード（プライベートリポジトリ対応、レート制限あり）
    /// </summary>
    public string? Token { get; set; }

    /// <summary>ポーリング間隔</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>pushed_atキャッシュ期間（この期間内は再チェックをスキップ）</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}

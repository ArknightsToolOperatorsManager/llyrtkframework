using llyrtkframework.Results;

namespace llyrtkframework.FileManagement.Core;

/// <summary>
/// ファイル管理のインターフェース
/// </summary>
/// <typeparam name="T">管理するデータの型</typeparam>
public interface IFileManager<T> : IFileManager where T : class
{
    /// <summary>ファイルからデータを非同期で読み込みます</summary>
    Task<Result<T>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>ファイルからデータを同期読み込みします</summary>
    Result<T> Load();

    /// <summary>データをファイルに非同期で保存します</summary>
    Task<Result> SaveAsync(T data, CancellationToken cancellationToken = default);

    /// <summary>データをファイルに同期保存します</summary>
    Result Save(T data);

    /// <summary>最新のバックアップからリストアします</summary>
    Task<Result<T>> RestoreFromLatestBackupAsync();

    /// <summary>ロールバック機能付きでバックアップからリストアします</summary>
    /// <param name="options">ロールバックオプション</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result<T>> RestoreWithRollbackAsync(RollbackOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>GitHubからファイルをダウンロードして保存します</summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result<T>> DownloadFromGitHubAsync(CancellationToken cancellationToken = default);

    /// <summary>GitHub上のファイルと同期（差分チェック＆更新）します</summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task<Result<T>> SyncWithGitHubAsync(CancellationToken cancellationToken = default);

    /// <summary>キャッシュされたデータを更新し、未保存フラグをセット</summary>
    void UpdateCachedData(T data);
}

/// <summary>
/// ファイル管理の非ジェネリックインターフェース
/// </summary>
public interface IFileManager : IDisposable
{
    /// <summary>ファイルパス</summary>
    string FilePath { get; }

    /// <summary>ファイルが存在するか</summary>
    bool FileExists { get; }

    /// <summary>最後のバックアップ以降に変更があるか</summary>
    bool HasChangesSinceBackup { get; }

    /// <summary>自動保存待ちのデータがあるか</summary>
    bool HasPendingAutoSave { get; }

    /// <summary>GitHub同期が有効かどうか</summary>
    bool IsGitHubSyncEnabled { get; }

    /// <summary>バックアップを作成します</summary>
    Task<Result> CreateBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>データが変更されたことをマーク（両方のフラグを立てる）</summary>
    void MarkAsChanged();

    /// <summary>自動保存フラグのみクリア</summary>
    void ClearAutoSaveFlag();

    /// <summary>バックアップフラグのみクリア</summary>
    void ClearBackupFlag();

    /// <summary>自動保存機能の有効/無効を設定</summary>
    void SetAutoSaveEnabled(bool enabled);

    /// <summary>自動保存が有効かどうか</summary>
    bool IsAutoSaveEnabled { get; }

    /// <summary>
    /// 自動保存を実行（キャッシュされたデータを使用）
    /// </summary>
    Task<Result> AutoSaveAsync(CancellationToken cancellationToken = default);
}

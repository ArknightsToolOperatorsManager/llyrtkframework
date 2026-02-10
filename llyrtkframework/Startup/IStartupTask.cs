using llyrtkframework.Results;

namespace llyrtkframework.Startup;

/// <summary>
/// 起動時に実行するタスクのインターフェース
/// </summary>
/// <remarks>
/// アプリケーション起動時に順次実行されるタスクを定義します。
/// 各タスクはOrderプロパティで実行順序を指定でき、Nameプロパティでタスクの名前を提供します。
/// </remarks>
public interface IStartupTask
{
    /// <summary>
    /// タスクの実行順序（小さいほど先に実行される）
    /// </summary>
    /// <remarks>
    /// 推奨値:
    /// - 10: データ読み込み
    /// - 20: 更新チェック
    /// - 30: 初期化処理
    /// - 40: UI準備
    /// </remarks>
    int Order { get; }

    /// <summary>
    /// タスク名（ログ・UI表示用）
    /// </summary>
    /// <remarks>
    /// ユーザーに表示されるタスクの名前を返します。
    /// 例: "データ読み込み", "更新確認", "初期化中"
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// タスクを実行します
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>実行結果</returns>
    /// <remarks>
    /// タスクの実行が失敗した場合は、Result.Failure を返します。
    /// 失敗した場合、後続のタスクは実行されません。
    /// </remarks>
    Task<Result> ExecuteAsync(CancellationToken cancellationToken = default);
}

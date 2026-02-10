using llyrtkframework.Results;

namespace llyrtkframework.StateManagement;

/// <summary>
/// グローバル状態管理ストアのインターフェース
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// 状態を取得します
    /// </summary>
    Result<T> GetState<T>(string key) where T : class;

    /// <summary>
    /// 状態を設定します
    /// </summary>
    Result SetState<T>(string key, T state) where T : class;

    /// <summary>
    /// 状態を削除します
    /// </summary>
    Result RemoveState(string key);

    /// <summary>
    /// 状態が存在するかチェックします
    /// </summary>
    bool ContainsState(string key);

    /// <summary>
    /// すべての状態をクリアします
    /// </summary>
    Result Clear();

    /// <summary>
    /// 状態の変更を監視します
    /// </summary>
    IObservable<StateChangedEventArgs> StateChanged { get; }
}

/// <summary>
/// 状態変更イベント引数
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public required string Key { get; init; }
    public required object? OldValue { get; init; }
    public required object? NewValue { get; init; }
}

using llyrtkframework.Results;
using System.Reactive.Linq;

namespace llyrtkframework.StateManagement;

/// <summary>
/// State Management関連の拡張メソッド
/// </summary>
public static class StateExtensions
{
    /// <summary>
    /// 状態を取得し、存在しない場合は設定します
    /// </summary>
    public static Result<T> GetOrSetState<T>(this IStateStore store, string key, T defaultState) where T : class
    {
        var result = store.GetState<T>(key);
        if (result.IsSuccess)
            return result;

        var setResult = store.SetState(key, defaultState);
        return setResult.IsSuccess
            ? Result<T>.Success(defaultState)
            : Result<T>.Failure(setResult.ErrorMessage ?? "Unknown error");
    }

    /// <summary>
    /// 特定のキーの状態変更を監視します
    /// </summary>
    public static IObservable<StateChangedEventArgs> WhenStateChanged(this IStateStore store, string key)
    {
        return store.StateChanged.Where(e => e.Key == key);
    }

    /// <summary>
    /// 特定のキーの状態変更を型付きで監視します
    /// </summary>
    public static IObservable<T> WhenStateChanged<T>(this IStateStore store, string key) where T : class
    {
        return store.StateChanged
            .Where(e => e.Key == key && e.NewValue is T)
            .Select(e => (T)e.NewValue!);
    }

    /// <summary>
    /// 状態を更新します
    /// </summary>
    public static Result UpdateState<T>(this IStateStore store, string key, Func<T, T> updateFunc) where T : class
    {
        var getResult = store.GetState<T>(key);
        if (getResult.IsFailure)
            return Result.Failure(getResult.ErrorMessage ?? "Unknown error");

        var newState = updateFunc(getResult.Value!);
        return store.SetState(key, newState);
    }
}

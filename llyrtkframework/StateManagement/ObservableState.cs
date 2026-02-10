using ReactiveUI;

namespace llyrtkframework.StateManagement;

/// <summary>
/// ReactiveUIベースの監視可能な状態オブジェクト
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public class ObservableState<T> : ReactiveObject
{
    private T _value;

    /// <summary>
    /// 状態の値
    /// </summary>
    public T Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public ObservableState(T initialValue)
    {
        _value = initialValue;
    }

    /// <summary>
    /// 状態を更新します
    /// </summary>
    public void Update(Func<T, T> updateFunc)
    {
        Value = updateFunc(_value);
    }

    /// <summary>
    /// 状態をリセットします
    /// </summary>
    public void Reset(T resetValue)
    {
        Value = resetValue;
    }

    public static implicit operator T(ObservableState<T> state) => state.Value;
}

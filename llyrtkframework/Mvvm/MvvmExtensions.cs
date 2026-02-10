using ReactiveUI;
using System.Reactive.Linq;

namespace llyrtkframework.Mvvm;

/// <summary>
/// MVVM関連の拡張メソッド
/// </summary>
public static class MvvmExtensions
{
    /// <summary>
    /// プロパティ変更を監視し、アクションを実行します
    /// </summary>
    public static IDisposable WhenPropertyChanged<T, TProperty>(
        this T source,
        System.Linq.Expressions.Expression<Func<T, TProperty>> property,
        Action<TProperty> action)
        where T : ReactiveObject
    {
        return source.WhenAnyValue(property)
            .Subscribe(action);
    }

    /// <summary>
    /// プロパティ変更を監視し、非同期アクションを実行します
    /// </summary>
    public static IDisposable WhenPropertyChangedAsync<T, TProperty>(
        this T source,
        System.Linq.Expressions.Expression<Func<T, TProperty>> property,
        Func<TProperty, Task> action)
        where T : ReactiveObject
    {
        return source.WhenAnyValue(property)
            .SelectMany(async value =>
            {
                await action(value);
                return value;
            })
            .Subscribe();
    }

    /// <summary>
    /// 複数のプロパティ変更を監視します
    /// </summary>
    public static IDisposable WhenAnyPropertyChanged<T>(
        this T source,
        Action action)
        where T : ReactiveObject
    {
        return source.Changed
            .Subscribe(_ => action());
    }

    /// <summary>
    /// デバウンス付きでプロパティ変更を監視します
    /// </summary>
    public static IDisposable WhenPropertyChangedThrottled<T, TProperty>(
        this T source,
        System.Linq.Expressions.Expression<Func<T, TProperty>> property,
        TimeSpan throttle,
        Action<TProperty> action)
        where T : ReactiveObject
    {
        return source.WhenAnyValue(property)
            .Throttle(throttle)
            .Subscribe(action);
    }
}

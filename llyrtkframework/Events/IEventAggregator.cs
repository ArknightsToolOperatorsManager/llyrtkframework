namespace llyrtkframework.Events;

/// <summary>
/// イベント集約のインターフェース
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// 指定された型のイベントストリームを取得します
    /// </summary>
    IObservable<TEvent> GetEvent<TEvent>();

    /// <summary>
    /// イベントを発行します
    /// </summary>
    void Publish<TEvent>(TEvent @event);

    /// <summary>
    /// 非同期でイベントを発行します
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}

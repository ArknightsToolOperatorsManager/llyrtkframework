using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace llyrtkframework.Events;

/// <summary>
/// ReactiveUI ベースのイベント集約実装
/// </summary>
public class EventAggregator : IEventAggregator, IDisposable
{
    private readonly Subject<object> _subject = new();
    private bool _disposed;

    /// <summary>
    /// 指定された型のイベントストリームを取得します
    /// </summary>
    public IObservable<TEvent> GetEvent<TEvent>()
    {
        return _subject.OfType<TEvent>();
    }

    /// <summary>
    /// イベントを発行します
    /// </summary>
    public void Publish<TEvent>(TEvent @event)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        _subject.OnNext(@event);
    }

    /// <summary>
    /// 非同期でイベントを発行します
    /// </summary>
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        Publish(@event);
        return Task.CompletedTask;
    }

    /// <summary>
    /// リソースを解放します
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _subject?.Dispose();
        }

        _disposed = true;
    }
}

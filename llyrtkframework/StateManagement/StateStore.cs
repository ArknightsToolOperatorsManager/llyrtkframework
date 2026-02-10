using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reactive.Subjects;

namespace llyrtkframework.StateManagement;

/// <summary>
/// グローバル状態管理ストアの実装
/// </summary>
public class StateStore : IStateStore, IDisposable
{
    private readonly ConcurrentDictionary<string, object> _states = new();
    private readonly Subject<StateChangedEventArgs> _stateChangedSubject = new();
    private readonly ILogger<StateStore>? _logger;

    public IObservable<StateChangedEventArgs> StateChanged => _stateChangedSubject;

    public StateStore(ILogger<StateStore>? logger = null)
    {
        _logger = logger;
    }

    public Result<T> GetState<T>(string key) where T : class
    {
        try
        {
            if (_states.TryGetValue(key, out var state))
            {
                if (state is T typedState)
                {
                    return Result<T>.Success(typedState);
                }

                return Result<T>.Failure($"State with key '{key}' is not of type {typeof(T).Name}");
            }

            return Result<T>.Failure($"State with key '{key}' not found");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting state for key {Key}", key);
            return Result<T>.FromException(ex, $"Error getting state for key '{key}'");
        }
    }

    public Result SetState<T>(string key, T state) where T : class
    {
        try
        {
            if (state == null)
                return Result.Failure("State cannot be null");

            var oldValue = _states.TryGetValue(key, out var old) ? old : null;
            _states[key] = state;

            _stateChangedSubject.OnNext(new StateChangedEventArgs
            {
                Key = key,
                OldValue = oldValue,
                NewValue = state
            });

            _logger?.LogDebug("State set: {Key}", key);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting state for key {Key}", key);
            return Result.FromException(ex, $"Error setting state for key '{key}'");
        }
    }

    public Result RemoveState(string key)
    {
        try
        {
            if (_states.TryRemove(key, out var oldValue))
            {
                _stateChangedSubject.OnNext(new StateChangedEventArgs
                {
                    Key = key,
                    OldValue = oldValue,
                    NewValue = null
                });

                _logger?.LogDebug("State removed: {Key}", key);
                return Result.Success();
            }

            return Result.Failure($"State with key '{key}' not found");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing state for key {Key}", key);
            return Result.FromException(ex, $"Error removing state for key '{key}'");
        }
    }

    public bool ContainsState(string key)
    {
        return _states.ContainsKey(key);
    }

    public Result Clear()
    {
        try
        {
            _states.Clear();
            _logger?.LogInformation("All states cleared");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing states");
            return Result.FromException(ex, "Error clearing states");
        }
    }

    public void Dispose()
    {
        _stateChangedSubject.OnCompleted();
        _stateChangedSubject.Dispose();
        GC.SuppressFinalize(this);
    }
}

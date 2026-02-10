using llyrtkframework.Configuration;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;

namespace llyrtkframework.StateManagement;

/// <summary>
/// StateStoreとConfigurationManagerを統合した永続化可能な状態管理
/// </summary>
public class PersistentStateManager : IDisposable
{
    private readonly IStateStore _stateStore;
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<PersistentStateManager>? _logger;
    private readonly Dictionary<string, bool> _persistentKeys = new();
    private IDisposable? _stateChangeSubscription;
    private bool _isAutoSaveEnabled = true;

    public PersistentStateManager(
        IStateStore stateStore,
        IConfigurationManager configManager,
        ILogger<PersistentStateManager>? logger = null)
    {
        _stateStore = stateStore;
        _configManager = configManager;
        _logger = logger;

        // StateStoreの変更を監視してConfigurationに同期
        _stateChangeSubscription = _stateStore.StateChanged
            .Where(e => _persistentKeys.ContainsKey(e.Key) && _persistentKeys[e.Key])
            .Throttle(TimeSpan.FromMilliseconds(500)) // デバウンス
            .Subscribe(async e =>
            {
                if (!_isAutoSaveEnabled)
                    return;

                try
                {
                    // 永続化が必要な状態のみConfigurationに保存
                    if (e.NewValue != null)
                    {
                        _configManager.SetValue(e.Key, e.NewValue);
                        await _configManager.SaveAsync();

                        _logger?.LogDebug("Persistent state auto-saved: {Key}", e.Key);
                    }
                    else
                    {
                        // 削除された場合
                        _configManager.RemoveValue(e.Key);
                        await _configManager.SaveAsync();

                        _logger?.LogDebug("Persistent state removed: {Key}", e.Key);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to auto-save persistent state: {Key}", e.Key);
                }
            });
    }

    /// <summary>
    /// 永続化される状態を設定（StateStore + Configuration の両方に保存）
    /// </summary>
    public Result SetPersistentState<T>(string key, T value) where T : class
    {
        try
        {
            _persistentKeys[key] = true;

            // StateStoreに設定（リアクティブ）
            var stateResult = _stateStore.SetState(key, value);
            if (stateResult.IsFailure)
                return stateResult;

            // Configurationにも設定（永続化）
            var configResult = _configManager.SetValue(key, value);
            if (configResult.IsFailure)
                return configResult;

            _logger?.LogDebug("Set persistent state: {Key}", key);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set persistent state: {Key}", key);
            return Result.FromException(ex, $"Failed to set persistent state: {key}");
        }
    }

    /// <summary>
    /// 一時的な状態を設定（StateStoreのみ、永続化なし）
    /// </summary>
    public Result SetTransientState<T>(string key, T value) where T : class
    {
        try
        {
            _persistentKeys[key] = false;
            var result = _stateStore.SetState(key, value);

            if (result.IsSuccess)
            {
                _logger?.LogDebug("Set transient state: {Key}", key);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set transient state: {Key}", key);
            return Result.FromException(ex, $"Failed to set transient state: {key}");
        }
    }

    /// <summary>
    /// 状態を取得（StateStoreから）
    /// </summary>
    public Result<T> GetState<T>(string key) where T : class
    {
        return _stateStore.GetState<T>(key);
    }

    /// <summary>
    /// 状態を削除
    /// </summary>
    public Result RemoveState(string key)
    {
        try
        {
            var stateResult = _stateStore.RemoveState(key);

            // 永続化されている場合はConfigurationからも削除
            if (_persistentKeys.ContainsKey(key) && _persistentKeys[key])
            {
                _configManager.RemoveValue(key);
                _persistentKeys.Remove(key);
            }

            return stateResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove state: {Key}", key);
            return Result.FromException(ex, $"Failed to remove state: {key}");
        }
    }

    /// <summary>
    /// 起動時にConfigurationからStateStoreへロード
    /// </summary>
    public async Task<Result> LoadPersistentStatesAsync()
    {
        try
        {
            var loadResult = await _configManager.LoadAsync();
            if (loadResult.IsFailure)
                return loadResult;

            _logger?.LogInformation("Persistent states loaded from configuration");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load persistent states");
            return Result.FromException(ex, "Failed to load persistent states");
        }
    }

    /// <summary>
    /// すべての永続状態を手動で保存
    /// </summary>
    public async Task<Result> SaveAllPersistentStatesAsync()
    {
        try
        {
            var result = await _configManager.SaveAsync();

            if (result.IsSuccess)
            {
                _logger?.LogInformation("All persistent states saved to configuration");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save all persistent states");
            return Result.FromException(ex, "Failed to save all persistent states");
        }
    }

    /// <summary>
    /// 自動保存の有効/無効を設定
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        _isAutoSaveEnabled = enabled;
        _logger?.LogInformation("Auto-save {Status}", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// 状態変更の監視ストリームを取得
    /// </summary>
    public IObservable<StateChangedEventArgs> StateChanged => _stateStore.StateChanged;

    public void Dispose()
    {
        _stateChangeSubscription?.Dispose();
        _stateChangeSubscription = null;
        GC.SuppressFinalize(this);
    }
}

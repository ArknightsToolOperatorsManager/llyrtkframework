using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reactive.Subjects;

namespace llyrtkframework.Localization;

/// <summary>
/// ローカライゼーションサービスの実装
/// </summary>
public class LocalizationService : ILocalizationService, IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _resources = new();
    private readonly Subject<CultureInfo> _cultureChangedSubject = new();
    private readonly ILogger<LocalizationService>? _logger;
    private CultureInfo _currentCulture;

    public CultureInfo CurrentCulture => _currentCulture;

    public IObservable<CultureInfo> CultureChanged => _cultureChangedSubject;

    public LocalizationService(ILogger<LocalizationService>? logger = null, CultureInfo? initialCulture = null)
    {
        _logger = logger;
        _currentCulture = initialCulture ?? CultureInfo.CurrentCulture;
    }

    public Result SetCulture(CultureInfo culture)
    {
        try
        {
            if (culture == null)
                return Result.Failure("Culture cannot be null");

            _currentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            _cultureChangedSubject.OnNext(culture);
            _logger?.LogInformation("Culture changed to: {Culture}", culture.Name);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting culture");
            return Result.FromException(ex, "Error setting culture");
        }
    }

    public Result SetCulture(string cultureName)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            return SetCulture(culture);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting culture with name: {CultureName}", cultureName);
            return Result.FromException(ex, $"Error setting culture with name: {cultureName}");
        }
    }

    public Result<string> GetString(string key)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
                return Result<string>.Failure("Key cannot be null or empty");

            var cultureName = _currentCulture.Name;

            if (_resources.TryGetValue(cultureName, out var cultureResources))
            {
                if (cultureResources.TryGetValue(key, out var value))
                {
                    return Result<string>.Success(value);
                }
            }

            // フォールバック: 親カルチャを試す
            if (!_currentCulture.IsNeutralCulture && _currentCulture.Parent != null)
            {
                var parentCultureName = _currentCulture.Parent.Name;
                if (_resources.TryGetValue(parentCultureName, out var parentResources))
                {
                    if (parentResources.TryGetValue(key, out var value))
                    {
                        return Result<string>.Success(value);
                    }
                }
            }

            _logger?.LogWarning("Localized string not found for key: {Key} in culture: {Culture}", key, cultureName);
            return Result<string>.Success($"[{key}]"); // キーをそのまま返す
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting localized string for key: {Key}", key);
            return Result<string>.FromException(ex, $"Error getting localized string for key: {key}");
        }
    }

    public Result<string> GetString(string key, params object[] args)
    {
        var result = GetString(key);

        if (result.IsFailure)
            return result;

        try
        {
            var formatted = string.Format(result.Value!, args);
            return Result<string>.Success(formatted);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error formatting localized string for key: {Key}", key);
            return Result<string>.FromException(ex, $"Error formatting localized string for key: {key}");
        }
    }

    public bool ContainsKey(string key)
    {
        var cultureName = _currentCulture.Name;

        if (_resources.TryGetValue(cultureName, out var cultureResources))
        {
            return cultureResources.ContainsKey(key);
        }

        return false;
    }

    public IEnumerable<CultureInfo> GetAvailableCultures()
    {
        return _resources.Keys.Select(name => CultureInfo.GetCultureInfo(name));
    }

    /// <summary>
    /// リソースを追加します
    /// </summary>
    public Result AddResource(string cultureName, string key, string value)
    {
        try
        {
            var cultureResources = _resources.GetOrAdd(cultureName, _ => new ConcurrentDictionary<string, string>());
            cultureResources[key] = value;

            _logger?.LogDebug("Resource added: {Culture}/{Key}", cultureName, key);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding resource");
            return Result.FromException(ex, "Error adding resource");
        }
    }

    /// <summary>
    /// リソースを一括追加します
    /// </summary>
    public Result AddResources(string cultureName, Dictionary<string, string> resources)
    {
        try
        {
            var cultureResources = _resources.GetOrAdd(cultureName, _ => new ConcurrentDictionary<string, string>());

            foreach (var kvp in resources)
            {
                cultureResources[kvp.Key] = kvp.Value;
            }

            _logger?.LogInformation("Resources added for culture: {Culture} ({Count} items)", cultureName, resources.Count);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding resources");
            return Result.FromException(ex, "Error adding resources");
        }
    }

    /// <summary>
    /// JSONファイルからリソースを読み込みます
    /// </summary>
    public async Task<Result> LoadResourcesFromFileAsync(string cultureName, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result.Failure($"Resource file not found: {filePath}");

            var json = await File.ReadAllTextAsync(filePath);
            var resources = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (resources == null)
                return Result.Failure("Failed to deserialize resource file");

            return AddResources(cultureName, resources);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading resources from file: {FilePath}", filePath);
            return Result.FromException(ex, $"Error loading resources from file: {filePath}");
        }
    }

    public void Dispose()
    {
        _cultureChangedSubject.OnCompleted();
        _cultureChangedSubject.Dispose();
        GC.SuppressFinalize(this);
    }
}

using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Serializers;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace llyrtkframework.Configuration;

/// <summary>
/// アプリケーション設定管理の実装
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
    private readonly ConcurrentDictionary<string, object> _settings = new();
    private readonly IFileManager<Dictionary<string, JsonElement>>? _fileManager;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly Dictionary<string, object> _defaults = new();

    public ConfigurationManager(ILogger<ConfigurationManager> logger, string? configFilePath = null)
    {
        _logger = logger;

        if (!string.IsNullOrEmpty(configFilePath))
        {
            _fileManager = new ConfigurationFileManager(
                configFilePath,
                new JsonFileSerializer<Dictionary<string, JsonElement>>(),
                logger
            );
        }
    }

    public Result<T> GetValue<T>(string key, T defaultValue = default!)
    {
        try
        {
            if (_settings.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                    return Result<T>.Success(typedValue);

                if (value is JsonElement jsonElement)
                {
                    var converted = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                    if (converted != null)
                    {
                        _settings[key] = converted;
                        return Result<T>.Success(converted);
                    }
                }

                return Result<T>.Failure($"Failed to convert value for key '{key}' to type {typeof(T).Name}");
            }

            return Result<T>.Success(defaultValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration value for key {Key}", key);
            return Result<T>.FromException(ex, $"Error getting configuration value for key '{key}'");
        }
    }

    public Result SetValue<T>(string key, T value)
    {
        try
        {
            if (value == null)
                return Result.Failure("Value cannot be null");

            _settings[key] = value;
            _logger.LogDebug("Configuration value set: {Key} = {Value}", key, value);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting configuration value for key {Key}", key);
            return Result.FromException(ex, $"Error setting configuration value for key '{key}'");
        }
    }

    public bool ContainsKey(string key)
    {
        return _settings.ContainsKey(key);
    }

    public Result RemoveValue(string key)
    {
        try
        {
            if (_settings.TryRemove(key, out _))
            {
                _logger.LogDebug("Configuration value removed: {Key}", key);
                return Result.Success();
            }

            return Result.Failure($"Key '{key}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing configuration value for key {Key}", key);
            return Result.FromException(ex, $"Error removing configuration value for key '{key}'");
        }
    }

    public Result Clear()
    {
        try
        {
            _settings.Clear();
            _logger.LogInformation("All configuration values cleared");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing configuration");
            return Result.FromException(ex, "Error clearing configuration");
        }
    }

    public async Task<Result> SaveAsync()
    {
        if (_fileManager == null)
            return Result.Failure("No file manager configured");

        try
        {
            var data = new Dictionary<string, JsonElement>();
            foreach (var kvp in _settings)
            {
                var json = JsonSerializer.Serialize(kvp.Value);
                data[kvp.Key] = JsonDocument.Parse(json).RootElement.Clone();
            }

            var result = await _fileManager.SaveAsync(data);
            if (result.IsSuccess)
            {
                _logger.LogInformation("Configuration saved successfully");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return Result.FromException(ex, "Error saving configuration");
        }
    }

    public async Task<Result> LoadAsync()
    {
        if (_fileManager == null)
            return Result.Failure("No file manager configured");

        try
        {
            var result = await _fileManager.LoadAsync();
            if (result.IsFailure)
                return Result.Failure(result.ErrorMessage ?? "Unknown error");

            _settings.Clear();
            foreach (var kvp in result.Value!)
            {
                _settings[kvp.Key] = kvp.Value;
            }

            _logger.LogInformation("Configuration loaded successfully with {Count} settings", _settings.Count);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration");
            return Result.FromException(ex, "Error loading configuration");
        }
    }

    public Result Reset()
    {
        try
        {
            _settings.Clear();
            foreach (var kvp in _defaults)
            {
                _settings[kvp.Key] = kvp.Value;
            }

            _logger.LogInformation("Configuration reset to default values");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration");
            return Result.FromException(ex, "Error resetting configuration");
        }
    }

    /// <summary>
    /// デフォルト値を設定します
    /// </summary>
    public Result SetDefaultValue<T>(string key, T value)
    {
        if (value == null)
            return Result.Failure("Default value cannot be null");

        _defaults[key] = value;
        return Result.Success();
    }

    private class ConfigurationFileManager : FileManagerBase<Dictionary<string, JsonElement>>
    {
        private readonly string _filePath;

        public ConfigurationFileManager(
            string filePath,
            IFileSerializer<Dictionary<string, JsonElement>> serializer,
            ILogger logger)
            : base(serializer, logger)
        {
            _filePath = filePath;
        }

        protected override string ConfigureFilePath() => _filePath;

        // デフォルトのバックアップ設定を使用
        // 必要に応じてオーバーライドして以下のメソッドを実装:
        // - protected override List<BackupTrigger> ConfigureBackupTriggers()
        // - protected override BackupOptions ConfigureBackupOptions()
        // - protected override GitHubFileOptions? ConfigureGitHubOptions()
    }
}

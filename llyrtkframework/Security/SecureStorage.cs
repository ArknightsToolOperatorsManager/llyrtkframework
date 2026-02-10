using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace llyrtkframework.Security;

/// <summary>
/// 機密データの暗号化ストレージ
/// DPAPI (Windows Data Protection API) を使用
/// </summary>
public class SecureStorage : ISecureStorage
{
    private readonly ILogger<SecureStorage> _logger;
    private readonly string _storagePath;

    public SecureStorage(ILogger<SecureStorage> logger, string storagePath)
    {
        _logger = logger;
        _storagePath = storagePath;

        // ストレージディレクトリを作成
        var directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// 文字列を暗号化して保存
    /// </summary>
    public async Task<Result> SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
                return Result.Failure("Key cannot be null or empty");

            if (string.IsNullOrEmpty(value))
                return Result.Failure("Value cannot be null or empty");

            // DPAPI で暗号化
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(value);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser
            );

            // ストレージに保存
            var storage = await LoadStorageAsync(cancellationToken);
            storage[key] = Convert.ToBase64String(encryptedBytes);

            await SaveStorageAsync(storage, cancellationToken);

            _logger.LogDebug("Secure data saved: {Key}", key);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save secure data: {Key}", key);
            return Result.FromException(ex, "Failed to save secure data");
        }
    }

    /// <summary>
    /// 暗号化されたデータを読み込んで復号化
    /// </summary>
    public async Task<Result<string>> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
                return Result<string>.Failure("Key cannot be null or empty");

            var storage = await LoadStorageAsync(cancellationToken);

            if (!storage.TryGetValue(key, out var encryptedBase64))
            {
                _logger.LogDebug("Secure data not found: {Key}", key);
                return Result<string>.Failure($"Key not found: {key}");
            }

            // DPAPI で復号化
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser
            );

            var value = System.Text.Encoding.UTF8.GetString(plainBytes);

            _logger.LogDebug("Secure data loaded: {Key}", key);
            return Result<string>.Success(value);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt secure data: {Key}", key);
            return Result<string>.Failure("Failed to decrypt secure data (invalid key or corrupted data)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load secure data: {Key}", key);
            return Result<string>.FromException(ex, "Failed to load secure data");
        }
    }

    /// <summary>
    /// データを削除
    /// </summary>
    public async Task<Result> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
                return Result.Failure("Key cannot be null or empty");

            var storage = await LoadStorageAsync(cancellationToken);

            if (!storage.Remove(key))
            {
                _logger.LogDebug("Secure data not found for deletion: {Key}", key);
                return Result.Failure($"Key not found: {key}");
            }

            await SaveStorageAsync(storage, cancellationToken);

            _logger.LogDebug("Secure data deleted: {Key}", key);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secure data: {Key}", key);
            return Result.FromException(ex, "Failed to delete secure data");
        }
    }

    /// <summary>
    /// キーが存在するかチェック
    /// </summary>
    public async Task<Result<bool>> ContainsKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
                return Result<bool>.Failure("Key cannot be null or empty");

            var storage = await LoadStorageAsync(cancellationToken);
            var exists = storage.ContainsKey(key);

            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check key existence: {Key}", key);
            return Result<bool>.FromException(ex, "Failed to check key existence");
        }
    }

    /// <summary>
    /// すべてのキーを取得
    /// </summary>
    public async Task<Result<IEnumerable<string>>> GetAllKeysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var storage = await LoadStorageAsync(cancellationToken);
            var keys = storage.Keys.ToList();

            return Result<IEnumerable<string>>.Success(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all keys");
            return Result<IEnumerable<string>>.FromException(ex, "Failed to get all keys");
        }
    }

    /// <summary>
    /// すべてのデータをクリア
    /// </summary>
    public async Task<Result> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveStorageAsync(new Dictionary<string, string>(), cancellationToken);

            _logger.LogInformation("Secure storage cleared");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear secure storage");
            return Result.FromException(ex, "Failed to clear secure storage");
        }
    }

    private async Task<Dictionary<string, string>> LoadStorageAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storagePath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            await using var stream = new FileStream(_storagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var storage = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken);
            return storage ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load storage file, returning empty storage");
            return new Dictionary<string, string>();
        }
    }

    private async Task SaveStorageAsync(Dictionary<string, string> storage, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        await using var stream = new FileStream(_storagePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, storage, options, cancellationToken);
    }
}

/// <summary>
/// セキュアストレージのインターフェース
/// </summary>
public interface ISecureStorage
{
    Task<Result> SaveAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<Result<string>> LoadAsync(string key, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<Result<bool>> ContainsKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<string>>> GetAllKeysAsync(CancellationToken cancellationToken = default);
    Task<Result> ClearAsync(CancellationToken cancellationToken = default);
}

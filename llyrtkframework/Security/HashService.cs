using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace llyrtkframework.Security;

/// <summary>
/// ハッシュ化サービス
/// SHA-256, SHA-512, HMAC をサポート
/// </summary>
public class HashService : IHashService
{
    private readonly ILogger<HashService> _logger;

    public HashService(ILogger<HashService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// SHA-256 ハッシュを計算
    /// </summary>
    public Result<byte[]> ComputeSha256(byte[] data)
    {
        try
        {
            if (data == null || data.Length == 0)
                return Result<byte[]>.Failure("Data cannot be null or empty");

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);

            _logger.LogDebug("SHA-256 hash computed ({Size} bytes)", hash.Length);
            return Result<byte[]>.Success(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute SHA-256 hash");
            return Result<byte[]>.FromException(ex, "Failed to compute SHA-256 hash");
        }
    }

    /// <summary>
    /// SHA-256 ハッシュを計算（文字列）
    /// </summary>
    public Result<string> ComputeSha256String(string data)
    {
        try
        {
            if (string.IsNullOrEmpty(data))
                return Result<string>.Failure("Data cannot be null or empty");

            var bytes = Encoding.UTF8.GetBytes(data);
            var hashResult = ComputeSha256(bytes);

            if (hashResult.IsFailure)
                return Result<string>.Failure(hashResult.ErrorMessage ?? "Unknown error");

            var hashString = Convert.ToHexString(hashResult.Value!).ToLowerInvariant();
            return Result<string>.Success(hashString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute SHA-256 hash string");
            return Result<string>.FromException(ex, "Failed to compute SHA-256 hash string");
        }
    }

    /// <summary>
    /// SHA-512 ハッシュを計算
    /// </summary>
    public Result<byte[]> ComputeSha512(byte[] data)
    {
        try
        {
            if (data == null || data.Length == 0)
                return Result<byte[]>.Failure("Data cannot be null or empty");

            using var sha512 = SHA512.Create();
            var hash = sha512.ComputeHash(data);

            _logger.LogDebug("SHA-512 hash computed ({Size} bytes)", hash.Length);
            return Result<byte[]>.Success(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute SHA-512 hash");
            return Result<byte[]>.FromException(ex, "Failed to compute SHA-512 hash");
        }
    }

    /// <summary>
    /// SHA-512 ハッシュを計算（文字列）
    /// </summary>
    public Result<string> ComputeSha512String(string data)
    {
        try
        {
            if (string.IsNullOrEmpty(data))
                return Result<string>.Failure("Data cannot be null or empty");

            var bytes = Encoding.UTF8.GetBytes(data);
            var hashResult = ComputeSha512(bytes);

            if (hashResult.IsFailure)
                return Result<string>.Failure(hashResult.ErrorMessage ?? "Unknown error");

            var hashString = Convert.ToHexString(hashResult.Value!).ToLowerInvariant();
            return Result<string>.Success(hashString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute SHA-512 hash string");
            return Result<string>.FromException(ex, "Failed to compute SHA-512 hash string");
        }
    }

    /// <summary>
    /// HMAC-SHA256 を計算
    /// </summary>
    public Result<byte[]> ComputeHmacSha256(byte[] data, byte[] key)
    {
        try
        {
            if (data == null || data.Length == 0)
                return Result<byte[]>.Failure("Data cannot be null or empty");

            if (key == null || key.Length == 0)
                return Result<byte[]>.Failure("Key cannot be null or empty");

            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(data);

            _logger.LogDebug("HMAC-SHA256 computed ({Size} bytes)", hash.Length);
            return Result<byte[]>.Success(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute HMAC-SHA256");
            return Result<byte[]>.FromException(ex, "Failed to compute HMAC-SHA256");
        }
    }

    /// <summary>
    /// HMAC-SHA256 を計算（文字列）
    /// </summary>
    public Result<string> ComputeHmacSha256String(string data, byte[] key)
    {
        try
        {
            if (string.IsNullOrEmpty(data))
                return Result<string>.Failure("Data cannot be null or empty");

            var bytes = Encoding.UTF8.GetBytes(data);
            var hmacResult = ComputeHmacSha256(bytes, key);

            if (hmacResult.IsFailure)
                return Result<string>.Failure(hmacResult.ErrorMessage ?? "Unknown error");

            var hmacString = Convert.ToHexString(hmacResult.Value!).ToLowerInvariant();
            return Result<string>.Success(hmacString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute HMAC-SHA256 string");
            return Result<string>.FromException(ex, "Failed to compute HMAC-SHA256 string");
        }
    }

    /// <summary>
    /// ファイルのSHA-256ハッシュを計算
    /// </summary>
    public async Task<Result<string>> ComputeFileSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<string>.Failure($"File not found: {filePath}");

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha256 = SHA256.Create();

            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();

            _logger.LogDebug("File SHA-256 hash computed: {FilePath}", filePath);
            return Result<string>.Success(hashString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute file SHA-256 hash: {FilePath}", filePath);
            return Result<string>.FromException(ex, "Failed to compute file SHA-256 hash");
        }
    }

    /// <summary>
    /// 2つのハッシュを比較（タイミング攻撃耐性あり）
    /// </summary>
    public bool CompareHashes(byte[] hash1, byte[] hash2)
    {
        if (hash1 == null || hash2 == null)
            return false;

        if (hash1.Length != hash2.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(hash1, hash2);
    }

    /// <summary>
    /// 2つのハッシュ文字列を比較（タイミング攻撃耐性あり）
    /// </summary>
    public bool CompareHashStrings(string hash1, string hash2)
    {
        if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2))
            return false;

        try
        {
            var bytes1 = Convert.FromHexString(hash1);
            var bytes2 = Convert.FromHexString(hash2);
            return CompareHashes(bytes1, bytes2);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// ハッシュサービスのインターフェース
/// </summary>
public interface IHashService
{
    Result<byte[]> ComputeSha256(byte[] data);
    Result<string> ComputeSha256String(string data);
    Result<byte[]> ComputeSha512(byte[] data);
    Result<string> ComputeSha512String(string data);
    Result<byte[]> ComputeHmacSha256(byte[] data, byte[] key);
    Result<string> ComputeHmacSha256String(string data, byte[] key);
    Task<Result<string>> ComputeFileSha256Async(string filePath, CancellationToken cancellationToken = default);
    bool CompareHashes(byte[] hash1, byte[] hash2);
    bool CompareHashStrings(string hash1, string hash2);
}

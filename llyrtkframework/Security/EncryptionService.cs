using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace llyrtkframework.Security;

/// <summary>
/// データ暗号化サービス
/// AES-256-GCM を使用した安全な暗号化
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly ILogger<EncryptionService> _logger;
    private const int KeySize = 32; // 256 bits
    private const int NonceSize = 12; // 96 bits (recommended for GCM)
    private const int TagSize = 16; // 128 bits

    public EncryptionService(ILogger<EncryptionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// データを暗号化
    /// </summary>
    public Result<byte[]> Encrypt(byte[] data, byte[] key)
    {
        try
        {
            if (data == null || data.Length == 0)
                return Result<byte[]>.Failure("Data cannot be null or empty");

            if (key == null || key.Length != KeySize)
                return Result<byte[]>.Failure($"Key must be exactly {KeySize} bytes");

            // Nonce (IV) を生成
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            // Tag 用のバッファ
            var tag = new byte[TagSize];

            // 暗号化
            var ciphertext = new byte[data.Length];
            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Encrypt(nonce, data, ciphertext, tag);

            // Nonce + Tag + Ciphertext の形式で結合
            var result = new byte[NonceSize + TagSize + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

            _logger.LogDebug("Data encrypted successfully ({Size} bytes)", result.Length);
            return Result<byte[]>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data");
            return Result<byte[]>.FromException(ex, "Failed to encrypt data");
        }
    }

    /// <summary>
    /// データを復号化
    /// </summary>
    public Result<byte[]> Decrypt(byte[] encryptedData, byte[] key)
    {
        try
        {
            if (encryptedData == null || encryptedData.Length < NonceSize + TagSize)
                return Result<byte[]>.Failure("Invalid encrypted data");

            if (key == null || key.Length != KeySize)
                return Result<byte[]>.Failure($"Key must be exactly {KeySize} bytes");

            // Nonce, Tag, Ciphertext を分離
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[encryptedData.Length - NonceSize - TagSize];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(encryptedData, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(encryptedData, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            // 復号化
            var plaintext = new byte[ciphertext.Length];
            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            _logger.LogDebug("Data decrypted successfully ({Size} bytes)", plaintext.Length);
            return Result<byte[]>.Success(plaintext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt data (authentication failed)");
            return Result<byte[]>.Failure("Failed to decrypt data: authentication failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data");
            return Result<byte[]>.FromException(ex, "Failed to decrypt data");
        }
    }

    /// <summary>
    /// 文字列を暗号化
    /// </summary>
    public Result<string> EncryptString(string plaintext, byte[] key)
    {
        try
        {
            if (string.IsNullOrEmpty(plaintext))
                return Result<string>.Failure("Plaintext cannot be null or empty");

            var data = Encoding.UTF8.GetBytes(plaintext);
            var encryptResult = Encrypt(data, key);

            if (encryptResult.IsFailure)
                return Result<string>.Failure(encryptResult.ErrorMessage ?? "Unknown error");

            var base64 = Convert.ToBase64String(encryptResult.Value!);
            return Result<string>.Success(base64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt string");
            return Result<string>.FromException(ex, "Failed to encrypt string");
        }
    }

    /// <summary>
    /// 文字列を復号化
    /// </summary>
    public Result<string> DecryptString(string ciphertext, byte[] key)
    {
        try
        {
            if (string.IsNullOrEmpty(ciphertext))
                return Result<string>.Failure("Ciphertext cannot be null or empty");

            var data = Convert.FromBase64String(ciphertext);
            var decryptResult = Decrypt(data, key);

            if (decryptResult.IsFailure)
                return Result<string>.Failure(decryptResult.ErrorMessage ?? "Unknown error");

            var plaintext = Encoding.UTF8.GetString(decryptResult.Value!);
            return Result<string>.Success(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt string");
            return Result<string>.FromException(ex, "Failed to decrypt string");
        }
    }

    /// <summary>
    /// ランダムな暗号化キーを生成
    /// </summary>
    public byte[] GenerateKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        _logger.LogDebug("Generated new encryption key");
        return key;
    }

    /// <summary>
    /// パスワードから暗号化キーを派生
    /// </summary>
    public Result<byte[]> DeriveKeyFromPassword(string password, byte[] salt, int iterations = 100000)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
                return Result<byte[]>.Failure("Password cannot be null or empty");

            if (salt == null || salt.Length < 16)
                return Result<byte[]>.Failure("Salt must be at least 16 bytes");

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256
            );

            var key = pbkdf2.GetBytes(KeySize);
            _logger.LogDebug("Derived key from password using PBKDF2");
            return Result<byte[]>.Success(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to derive key from password");
            return Result<byte[]>.FromException(ex, "Failed to derive key from password");
        }
    }

    /// <summary>
    /// ランダムなソルトを生成
    /// </summary>
    public byte[] GenerateSalt(int size = 16)
    {
        var salt = new byte[size];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }
}

/// <summary>
/// 暗号化サービスのインターフェース
/// </summary>
public interface IEncryptionService
{
    Result<byte[]> Encrypt(byte[] data, byte[] key);
    Result<byte[]> Decrypt(byte[] encryptedData, byte[] key);
    Result<string> EncryptString(string plaintext, byte[] key);
    Result<string> DecryptString(string ciphertext, byte[] key);
    byte[] GenerateKey();
    Result<byte[]> DeriveKeyFromPassword(string password, byte[] salt, int iterations = 100000);
    byte[] GenerateSalt(int size = 16);
}

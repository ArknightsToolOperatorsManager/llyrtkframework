using System.Security.Cryptography;
using System.Text;

namespace llyrtkframework.FileManagement.Utilities;

/// <summary>
/// SHA256ハッシュ計算ユーティリティ
/// </summary>
public static class HashUtility
{
    /// <summary>
    /// ファイルからSHA256ハッシュを非同期で計算します
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>SHA256ハッシュ（小文字16進数文字列）</returns>
    public static async Task<string> CalculateSha256FromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// バイト配列からSHA256ハッシュを計算します
    /// </summary>
    /// <param name="bytes">バイト配列</param>
    /// <returns>SHA256ハッシュ（小文字16進数文字列）</returns>
    public static string CalculateSha256FromBytes(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(bytes);

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 文字列からSHA256ハッシュを計算します
    /// </summary>
    /// <param name="content">文字列（UTF-8エンコーディング）</param>
    /// <returns>SHA256ハッシュ（小文字16進数文字列）</returns>
    public static string CalculateSha256FromString(string content)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        var bytes = Encoding.UTF8.GetBytes(content);
        return CalculateSha256FromBytes(bytes);
    }
}

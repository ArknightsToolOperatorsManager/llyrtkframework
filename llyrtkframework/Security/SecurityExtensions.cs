using System.Security;
using System.Runtime.InteropServices;

namespace llyrtkframework.Security;

/// <summary>
/// セキュリティ関連の拡張メソッド
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// 文字列を SecureString に変換
    /// </summary>
    public static SecureString ToSecureString(this string source)
    {
        if (string.IsNullOrEmpty(source))
            return new SecureString();

        var secureString = new SecureString();
        foreach (var c in source)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        return secureString;
    }

    /// <summary>
    /// SecureString を通常の文字列に変換
    /// 使用後は必ずメモリをクリアすること
    /// </summary>
    public static string ToUnsecureString(this SecureString secureString)
    {
        if (secureString == null)
            return string.Empty;

        var ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
            return Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
    }

    /// <summary>
    /// バイト配列をゼロクリア
    /// </summary>
    public static void Clear(this byte[] array)
    {
        if (array != null)
        {
            Array.Clear(array, 0, array.Length);
        }
    }

    /// <summary>
    /// バイト配列を16進数文字列に変換
    /// </summary>
    public static string ToHexString(this byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 16進数文字列をバイト配列に変換
    /// </summary>
    public static byte[] FromHexString(this string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Array.Empty<byte>();

        return Convert.FromHexString(hex);
    }

    /// <summary>
    /// Base64エンコード
    /// </summary>
    public static string ToBase64(this byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Base64デコード
    /// </summary>
    public static byte[] FromBase64(this string base64)
    {
        if (string.IsNullOrEmpty(base64))
            return Array.Empty<byte>();

        return Convert.FromBase64String(base64);
    }
}

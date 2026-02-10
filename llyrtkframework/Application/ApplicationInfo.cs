using System.Reflection;

namespace llyrtkframework.Application;

/// <summary>
/// アプリケーション情報
/// </summary>
public class ApplicationInfo
{
    /// <summary>
    /// アプリケーション名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// アプリケーションバージョン
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// ビルド日時
    /// </summary>
    public DateTime BuildDate { get; }

    /// <summary>
    /// 会社名
    /// </summary>
    public string Company { get; }

    /// <summary>
    /// 著作権
    /// </summary>
    public string Copyright { get; }

    /// <summary>
    /// 説明
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// アプリケーションデータディレクトリ
    /// </summary>
    public string ApplicationDataPath { get; }

    /// <summary>
    /// 実行ディレクトリ
    /// </summary>
    public string ExecutableDirectory { get; }

    /// <summary>
    /// 実行ファイルパス
    /// </summary>
    public string ExecutablePath { get; }

    public ApplicationInfo()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        Name = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
               ?? assembly.GetName().Name
               ?? "Unknown";

        Version = assembly.GetName().Version ?? new Version(1, 0, 0, 0);

        Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
        Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;

        // ビルド日時を取得（リンカータイムスタンプから）
        BuildDate = GetBuildDate(assembly);

        // パス情報
        ExecutablePath = Environment.ProcessPath ?? assembly.Location;
        ExecutableDirectory = Path.GetDirectoryName(ExecutablePath) ?? Environment.CurrentDirectory;
        ApplicationDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Name
        );
    }

    /// <summary>
    /// カスタム設定でインスタンスを作成
    /// </summary>
    public ApplicationInfo(
        string name,
        Version version,
        string company = "",
        string copyright = "",
        string description = "")
    {
        Name = name;
        Version = version;
        Company = company;
        Copyright = copyright;
        Description = description;

        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        BuildDate = GetBuildDate(assembly);

        ExecutablePath = Environment.ProcessPath ?? assembly.Location;
        ExecutableDirectory = Path.GetDirectoryName(ExecutablePath) ?? Environment.CurrentDirectory;
        ApplicationDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Name
        );
    }

    /// <summary>
    /// バージョン文字列を取得
    /// </summary>
    public string GetVersionString() => Version.ToString();

    /// <summary>
    /// フルバージョン文字列を取得（名前 + バージョン）
    /// </summary>
    public string GetFullVersionString() => $"{Name} {Version}";

    /// <summary>
    /// アプリケーション情報の文字列表現
    /// </summary>
    public override string ToString()
    {
        return $"{Name} v{Version} ({BuildDate:yyyy-MM-dd})";
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        try
        {
            // AssemblyInformationalVersionAttribute から取得を試みる
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(infoVersion))
            {
                // Git情報などが含まれる場合は解析
                var parts = infoVersion.Split('+');
                if (parts.Length > 1)
                {
                    // ビルド日時が含まれていれば使用
                    // 例: "1.0.0+20240101.1"
                }
            }

            // ファイル作成日時をフォールバック
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                return File.GetCreationTimeUtc(location);
            }
        }
        catch
        {
            // エラーの場合は現在時刻を返す
        }

        return DateTime.UtcNow;
    }
}

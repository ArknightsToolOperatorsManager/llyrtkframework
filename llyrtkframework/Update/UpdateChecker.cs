using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace llyrtkframework.Update;

/// <summary>
/// GitHub Releases からアプリケーション更新をチェック
/// </summary>
public class UpdateChecker : IUpdateChecker
{
    private readonly ILogger<UpdateChecker> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repository;

    public UpdateChecker(
        ILogger<UpdateChecker> logger,
        HttpClient httpClient,
        string owner,
        string repository)
    {
        _logger = logger;
        _httpClient = httpClient;
        _owner = owner;
        _repository = repository;

        // GitHub API用のヘッダー
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("llyrtkframework-update-checker/1.0");
    }

    /// <summary>
    /// 更新をチェック
    /// </summary>
    public async Task<Result<UpdateInfo>> CheckForUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates (current version: {Version})", currentVersion);

            // GitHub API: 最新リリースを取得
            var apiUrl = $"https://api.github.com/repos/{_owner}/{_repository}/releases/latest";

            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(apiUrl, cancellationToken);
            if (release == null)
            {
                _logger.LogWarning("No releases found");
                return Result<UpdateInfo>.Success(new UpdateInfo
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = currentVersion
                });
            }

            // バージョンを解析
            var tagName = release.TagName.TrimStart('v');
            if (!Version.TryParse(tagName, out var latestVersion))
            {
                _logger.LogWarning("Failed to parse version from tag: {TagName}", release.TagName);
                return Result<UpdateInfo>.Failure($"Invalid version tag: {release.TagName}");
            }

            // 更新が必要かチェック
            var isUpdateAvailable = latestVersion > currentVersion;

            _logger.LogInformation(
                "Update check completed: Latest={Latest}, Current={Current}, UpdateAvailable={UpdateAvailable}",
                latestVersion,
                currentVersion,
                isUpdateAvailable
            );

            var updateInfo = new UpdateInfo
            {
                IsUpdateAvailable = isUpdateAvailable,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                ReleaseName = release.Name,
                ReleaseNotes = release.Body,
                PublishedAt = release.PublishedAt,
                DownloadUrl = release.HtmlUrl,
                Assets = release.Assets.Select(a => new ReleaseAsset
                {
                    Name = a.Name,
                    Size = a.Size,
                    DownloadUrl = a.BrowserDownloadUrl,
                    ContentType = a.ContentType
                }).ToList()
            };

            return Result<UpdateInfo>.Success(updateInfo);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check for updates (network error)");
            return Result<UpdateInfo>.FromException(ex, "Failed to check for updates: network error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return Result<UpdateInfo>.FromException(ex, "Failed to check for updates");
        }
    }

    /// <summary>
    /// すべてのリリースを取得
    /// </summary>
    public async Task<Result<IEnumerable<UpdateInfo>>> GetAllReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching all releases");

            var apiUrl = $"https://api.github.com/repos/{_owner}/{_repository}/releases";

            var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(apiUrl, cancellationToken);
            if (releases == null || releases.Count == 0)
            {
                _logger.LogWarning("No releases found");
                return Result<IEnumerable<UpdateInfo>>.Success(Enumerable.Empty<UpdateInfo>());
            }

            var updateInfos = new List<UpdateInfo>();
            foreach (var release in releases)
            {
                var tagName = release.TagName.TrimStart('v');
                if (Version.TryParse(tagName, out var version))
                {
                    updateInfos.Add(new UpdateInfo
                    {
                        IsUpdateAvailable = false, // N/A for list
                        CurrentVersion = new Version(0, 0, 0),
                        LatestVersion = version,
                        ReleaseName = release.Name,
                        ReleaseNotes = release.Body,
                        PublishedAt = release.PublishedAt,
                        DownloadUrl = release.HtmlUrl,
                        Assets = release.Assets.Select(a => new ReleaseAsset
                        {
                            Name = a.Name,
                            Size = a.Size,
                            DownloadUrl = a.BrowserDownloadUrl,
                            ContentType = a.ContentType
                        }).ToList()
                    });
                }
            }

            _logger.LogInformation("Fetched {Count} releases", updateInfos.Count);
            return Result<IEnumerable<UpdateInfo>>.Success(updateInfos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get releases");
            return Result<IEnumerable<UpdateInfo>>.FromException(ex, "Failed to get releases");
        }
    }
}

/// <summary>
/// 更新チェッカーのインターフェース
/// </summary>
public interface IUpdateChecker
{
    Task<Result<UpdateInfo>> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<UpdateInfo>>> GetAllReleasesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 更新情報
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// 更新が利用可能か
    /// </summary>
    public bool IsUpdateAvailable { get; set; }

    /// <summary>
    /// 現在のバージョン
    /// </summary>
    public Version CurrentVersion { get; set; } = new Version(0, 0, 0);

    /// <summary>
    /// 最新バージョン
    /// </summary>
    public Version LatestVersion { get; set; } = new Version(0, 0, 0);

    /// <summary>
    /// リリース名
    /// </summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>
    /// リリースノート
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 公開日時
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// ダウンロードURL（GitHubリリースページ）
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// リリースアセット
    /// </summary>
    public List<ReleaseAsset> Assets { get; set; } = new();
}

/// <summary>
/// リリースアセット
/// </summary>
public class ReleaseAsset
{
    /// <summary>
    /// ファイル名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ファイルサイズ（バイト）
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// ダウンロードURL
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// コンテンツタイプ
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>
/// GitHub Release APIのレスポンス
/// </summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

/// <summary>
/// GitHub Asset APIのレスポンス
/// </summary>
internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;
}

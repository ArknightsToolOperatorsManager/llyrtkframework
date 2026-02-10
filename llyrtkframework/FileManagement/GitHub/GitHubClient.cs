using llyrtkframework.FileManagement.Utilities;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace llyrtkframework.FileManagement.GitHub;

/// <summary>
/// GitHub API呼び出しクライアント
/// </summary>
public class GitHubClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private bool _disposed;

    public GitHubClient(ILogger logger, HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("llyrtkframework", "1.0"));
        _httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true
        };
    }

    /// <summary>
    /// リポジトリの最終更新日時（pushed_at）を取得します
    /// </summary>
    public async Task<Result<DateTime>> GetRepositoryLastPushedAtAsync(
        string owner,
        string repository,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repository}";
            var request = CreateRequest(HttpMethod.Get, url, token);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new GitHubApiException(
                    $"GitHub API error: {response.StatusCode}",
                    (int)response.StatusCode,
                    content);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("pushed_at", out var pushedAtElement))
            {
                var pushedAtStr = pushedAtElement.GetString();
                if (DateTime.TryParse(pushedAtStr, out var pushedAt))
                {
                    return Result<DateTime>.Success(pushedAt.ToUniversalTime());
                }
            }

            return Result<DateTime>.Failure("pushed_at property not found in response");
        }
        catch (GitHubApiException ex)
        {
            _logger.LogError(ex, "GitHub API error for {Owner}/{Repo}", owner, repository);
            return Result<DateTime>.FromException(ex, "GitHub API call failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get repository info for {Owner}/{Repo}", owner, repository);
            return Result<DateTime>.FromException(ex, "Failed to get repository info");
        }
    }

    /// <summary>
    /// ファイル内容を取得します
    /// トークンが提供されている場合はGitHub API経由、そうでない場合はraw.githubusercontent.com経由で取得します
    /// </summary>
    public async Task<Result<string>> GetFileContentAsync(
        string owner,
        string repository,
        string branch,
        string filePath,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // トークンがない場合はraw.githubusercontent.comから直接取得（レート制限なし）
            if (string.IsNullOrEmpty(token))
            {
                var rawUrl = $"https://raw.githubusercontent.com/{owner}/{repository}/{branch}/{filePath}";
                _logger.LogDebug("Downloading from raw URL (no token): {Url}", rawUrl);

                var response = await _httpClient.GetAsync(rawUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    return Result<string>.Failure(
                        $"Failed to download file from GitHub (Status: {response.StatusCode}): {errorContent}");
                }

                var fileContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Successfully downloaded file from raw URL");
                return Result<string>.Success(fileContent);
            }

            // トークンがある場合はGitHub API経由で取得（プライベートリポジトリ対応）
            var url = $"https://api.github.com/repos/{owner}/{repository}/contents/{filePath}?ref={branch}";
            var request = CreateRequest(HttpMethod.Get, url, token);

            var apiResponse = await _httpClient.SendAsync(request, cancellationToken);

            if (!apiResponse.IsSuccessStatusCode)
            {
                var content = await apiResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new GitHubApiException(
                    $"GitHub API error: {apiResponse.StatusCode}",
                    (int)apiResponse.StatusCode,
                    content);
            }

            var json = await apiResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // download_urlを取得して直接ダウンロード
            if (doc.RootElement.TryGetProperty("download_url", out var downloadUrlElement))
            {
                var downloadUrl = downloadUrlElement.GetString();
                if (string.IsNullOrEmpty(downloadUrl))
                    return Result<string>.Failure("download_url is empty");

                var downloadResponse = await _httpClient.GetAsync(downloadUrl, cancellationToken);
                downloadResponse.EnsureSuccessStatusCode();

                var fileContent = await downloadResponse.Content.ReadAsStringAsync(cancellationToken);
                return Result<string>.Success(fileContent);
            }

            return Result<string>.Failure("download_url property not found in response");
        }
        catch (GitHubApiException ex)
        {
            _logger.LogError(ex, "GitHub API error for {Owner}/{Repo}/{Path}", owner, repository, filePath);
            return Result<string>.FromException(ex, "GitHub API call failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file content for {Owner}/{Repo}/{Path}", owner, repository, filePath);
            return Result<string>.FromException(ex, "Failed to get file content");
        }
    }

    /// <summary>
    /// ファイルのSHA256ハッシュを取得します（内容をダウンロードしてハッシュ計算）
    /// </summary>
    public async Task<Result<string>> GetFileSha256Async(
        string owner,
        string repository,
        string branch,
        string filePath,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contentResult = await GetFileContentAsync(owner, repository, branch, filePath, token, cancellationToken);

            if (contentResult.IsFailure)
                return Result<string>.Failure(contentResult.ErrorMessage!);

            var hash = HashUtility.CalculateSha256FromString(contentResult.Value);
            return Result<string>.Success(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate SHA256 for {Owner}/{Repo}/{Path}", owner, repository, filePath);
            return Result<string>.FromException(ex, "Failed to calculate SHA256");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
        }

        return request;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

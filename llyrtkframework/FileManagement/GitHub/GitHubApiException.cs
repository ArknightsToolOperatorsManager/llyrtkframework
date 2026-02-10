namespace llyrtkframework.FileManagement.GitHub;

/// <summary>
/// GitHub API固有の例外
/// </summary>
public class GitHubApiException : Exception
{
    /// <summary>HTTPステータスコード</summary>
    public int? StatusCode { get; }

    /// <summary>GitHub APIレスポンスメッセージ</summary>
    public string? ResponseMessage { get; }

    public GitHubApiException(string message) : base(message)
    {
    }

    public GitHubApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GitHubApiException(string message, int statusCode, string? responseMessage = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseMessage = responseMessage;
    }
}

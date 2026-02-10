using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace llyrtkframework.Application;

/// <summary>
/// アプリケーションの起動制御を管理
/// 同じフォルダからの重複起動を防止（別フォルダからは許可）
/// </summary>
public class ApplicationInstanceManager : IDisposable
{
    private readonly ILogger<ApplicationInstanceManager> _logger;
    private Mutex? _mutex;
    private bool _mutexOwned;

    public ApplicationInstanceManager(ILogger<ApplicationInstanceManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// アプリケーションインスタンスを取得
    /// 同じフォルダから既に起動している場合は失敗
    /// </summary>
    public Result<bool> TryAcquireInstance()
    {
        try
        {
            // 実行パスからユニークなMutex名を生成
            var executablePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;
            var mutexName = GenerateMutexName(executablePath);

            _logger.LogInformation("Attempting to acquire application instance with mutex: {MutexName}", mutexName);

            _mutex = new Mutex(true, mutexName, out _mutexOwned);

            if (!_mutexOwned)
            {
                _logger.LogWarning("Another instance is already running from the same folder: {Path}", executablePath);
                return Result<bool>.Success(false);
            }

            _logger.LogInformation("Application instance acquired successfully");
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire application instance");
            return Result<bool>.FromException(ex, "Failed to acquire application instance");
        }
    }

    /// <summary>
    /// アプリケーションインスタンスを解放
    /// </summary>
    public void ReleaseInstance()
    {
        if (_mutexOwned && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
                _mutexOwned = false;
                _logger.LogInformation("Application instance released");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release application instance");
            }
        }
    }

    /// <summary>
    /// 実行パスからユニークなMutex名を生成
    /// </summary>
    private static string GenerateMutexName(string executablePath)
    {
        // パスを正規化
        var normalizedPath = Path.GetFullPath(executablePath).ToLowerInvariant();

        // SHA256ハッシュを生成
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedPath));
        var hashString = Convert.ToHexString(hashBytes);

        // Global\ プレフィックスを付けてシステムワイドにする
        return $"Global\\LlyrtkFramework_{hashString}";
    }

    public void Dispose()
    {
        ReleaseInstance();
        _mutex?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Pre-bootタスク: アプリケーションインスタンスチェック
/// </summary>
public class CheckApplicationInstanceTask : IPreBootTask
{
    private readonly ApplicationInstanceManager _instanceManager;
    private readonly ILogger<CheckApplicationInstanceTask> _logger;

    public CheckApplicationInstanceTask(
        ApplicationInstanceManager instanceManager,
        ILogger<CheckApplicationInstanceTask> logger)
    {
        _instanceManager = instanceManager;
        _logger = logger;
    }

    public Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking application instance");

        var result = _instanceManager.TryAcquireInstance();
        if (result.IsFailure)
        {
            return Task.FromResult(Result.Failure(result.ErrorMessage ?? "Unknown error"));
        }

        if (!result.Value)
        {
            _logger.LogWarning("Another instance is already running from the same folder");
            return Task.FromResult(Result.Failure("Another instance is already running from the same folder"));
        }

        return Task.FromResult(Result.Success());
    }
}

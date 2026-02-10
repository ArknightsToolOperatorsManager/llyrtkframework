using llyrtkframework.FileManagement.Core;
using llyrtkframework.FileManagement.Events;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using EventAggregator = llyrtkframework.Events.IEventAggregator;

namespace llyrtkframework.FileManagement.Services;

/// <summary>
/// ファイルI/O操作を提供するサービス実装
/// </summary>
public class FileIOService<T> : IFileIOService<T> where T : class
{
    private readonly IFileSerializer<T> _serializer;
    private readonly ILogger _logger;
    private readonly string _filePath;
    private readonly EventAggregator? _eventAggregator;
    private readonly ReaderWriterLockSlim _lock = new();

    public FileIOService(
        IFileSerializer<T> serializer,
        ILogger logger,
        string filePath,
        EventAggregator? eventAggregator = null)
    {
        _serializer = serializer;
        _logger = logger;
        _filePath = filePath;
        _eventAggregator = eventAggregator;
    }

    public async Task<Result<T>> LoadAsync(
        Action<T>? onSuccess = null,
        CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", _filePath);
                return Result<T>.Failure($"File not found: {_filePath}");
            }

            var content = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var data = _serializer.Deserialize(content);

            _eventAggregator?.Publish(new FileLoadedEvent(_filePath));
            _logger.LogDebug("Loaded file: {FilePath}", _filePath);

            onSuccess?.Invoke(data);

            return Result<T>.Success(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load file: {FilePath}", _filePath);
            return Result<T>.FromException(ex, $"Failed to load file: {_filePath}");
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Result<T> Load(Action<T>? onSuccess = null)
    {
        return LoadAsync(onSuccess).GetAwaiter().GetResult();
    }

    public async Task<Result> SaveAsync(
        T data,
        Action? onSuccess = null,
        CancellationToken cancellationToken = default)
    {
        _lock.EnterWriteLock();
        try
        {
            // 一時ファイルに書き込み（トランザクション）
            var tempFile = _filePath + ".tmp";
            var serialized = _serializer.Serialize(data);

            await File.WriteAllTextAsync(tempFile, serialized, cancellationToken);

            // 一時ファイルを本番ファイルに置き換え（アトミック操作）
            File.Move(tempFile, _filePath, overwrite: true);

            _eventAggregator?.Publish(new FileSavedEvent(_filePath));
            _logger.LogDebug("Saved file: {FilePath}", _filePath);

            onSuccess?.Invoke();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {FilePath}", _filePath);
            return Result.FromException(ex, $"Failed to save file: {_filePath}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Result Save(T data, Action? onSuccess = null)
    {
        return SaveAsync(data, onSuccess).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}

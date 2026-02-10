using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace llyrtkframework.Notifications;

/// <summary>
/// 通知キューの管理
/// </summary>
public class NotificationQueue : IDisposable
{
    private readonly ConcurrentQueue<Notification> _queue = new();
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationQueue>? _logger;
    private readonly Timer _timer;
    private readonly TimeSpan _processInterval;
    private bool _isProcessing;

    public int Count => _queue.Count;

    public NotificationQueue(
        INotificationService notificationService,
        TimeSpan? processInterval = null,
        ILogger<NotificationQueue>? logger = null)
    {
        _notificationService = notificationService;
        _logger = logger;
        _processInterval = processInterval ?? TimeSpan.FromSeconds(1);

        _timer = new Timer(async _ => await ProcessQueueAsync(), null, _processInterval, _processInterval);
    }

    /// <summary>
    /// 通知をキューに追加します
    /// </summary>
    public Result Enqueue(Notification notification)
    {
        try
        {
            if (notification == null)
                return Result.Failure("Notification cannot be null");

            _queue.Enqueue(notification);
            _logger?.LogDebug("Notification enqueued: {Title}", notification.Title);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error enqueueing notification");
            return Result.FromException(ex, "Error enqueueing notification");
        }
    }

    /// <summary>
    /// キューから通知を取り出します
    /// </summary>
    public Result<Notification> Dequeue()
    {
        try
        {
            if (_queue.TryDequeue(out var notification))
            {
                return Result<Notification>.Success(notification);
            }

            return Result<Notification>.Failure("Queue is empty");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error dequeuing notification");
            return Result<Notification>.FromException(ex, "Error dequeuing notification");
        }
    }

    /// <summary>
    /// キューをクリアします
    /// </summary>
    public Result Clear()
    {
        try
        {
            _queue.Clear();
            _logger?.LogInformation("Notification queue cleared");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing notification queue");
            return Result.FromException(ex, "Error clearing notification queue");
        }
    }

    /// <summary>
    /// キューを処理します
    /// </summary>
    public async Task<Result> ProcessQueueAsync()
    {
        if (_isProcessing)
            return Result.Success();

        _isProcessing = true;

        try
        {
            while (_queue.TryDequeue(out var notification))
            {
                var result = await _notificationService.SendAsync(notification);
                if (result.IsFailure)
                {
                    _logger?.LogWarning("Failed to send notification: {Error}", result.ErrorMessage);
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing notification queue");
            return Result.FromException(ex, "Error processing notification queue");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}

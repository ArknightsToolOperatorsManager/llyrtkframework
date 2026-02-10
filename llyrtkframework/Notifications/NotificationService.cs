using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace llyrtkframework.Notifications;

/// <summary>
/// 通知サービスの実装
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    private readonly Subject<Notification> _notificationSubject = new();
    private readonly ILogger<NotificationService>? _logger;

    public IObservable<Notification> Notifications => _notificationSubject.AsObservable();

    public NotificationService(ILogger<NotificationService>? logger = null)
    {
        _logger = logger;
    }

    public Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            if (notification == null)
                return Task.FromResult(Result.Failure("Notification cannot be null"));

            _notificationSubject.OnNext(notification);
            _logger?.LogDebug("Notification sent: {Title} ({Type})", notification.Title, notification.Type);

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending notification");
            return Task.FromResult(Result.FromException(ex, "Error sending notification"));
        }
    }

    public Task<Result> SendAsync(string title, string message, NotificationType type = NotificationType.Information)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Message = message,
            Type = type
        };

        return SendAsync(notification);
    }

    public IObservable<Notification> GetNotificationsByType(NotificationType type)
    {
        return _notificationSubject.Where(n => n.Type == type);
    }

    public void Dispose()
    {
        _notificationSubject.OnCompleted();
        _notificationSubject.Dispose();
        GC.SuppressFinalize(this);
    }
}

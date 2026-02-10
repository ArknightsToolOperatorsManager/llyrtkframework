using System.Reactive.Linq;

namespace llyrtkframework.Notifications;

/// <summary>
/// Notification関連の拡張メソッド
/// </summary>
public static class NotificationExtensions
{
    /// <summary>
    /// エラー通知のみをフィルタリングします
    /// </summary>
    public static IObservable<Notification> Errors(this INotificationService service)
    {
        return service.GetNotificationsByType(NotificationType.Error);
    }

    /// <summary>
    /// 警告通知のみをフィルタリングします
    /// </summary>
    public static IObservable<Notification> Warnings(this INotificationService service)
    {
        return service.GetNotificationsByType(NotificationType.Warning);
    }

    /// <summary>
    /// 成功通知のみをフィルタリングします
    /// </summary>
    public static IObservable<Notification> Successes(this INotificationService service)
    {
        return service.GetNotificationsByType(NotificationType.Success);
    }

    /// <summary>
    /// 情報通知のみをフィルタリングします
    /// </summary>
    public static IObservable<Notification> Informations(this INotificationService service)
    {
        return service.GetNotificationsByType(NotificationType.Information);
    }

    /// <summary>
    /// 未読の通知のみをフィルタリングします
    /// </summary>
    public static IObservable<Notification> Unread(this IObservable<Notification> notifications)
    {
        return notifications.Where(n => !n.IsRead);
    }

    /// <summary>
    /// 通知をデバウンスします
    /// </summary>
    public static IObservable<Notification> Debounce(this IObservable<Notification> notifications, TimeSpan duration)
    {
        return notifications.Throttle(duration);
    }
}

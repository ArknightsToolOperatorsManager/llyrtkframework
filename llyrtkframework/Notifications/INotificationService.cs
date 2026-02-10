using llyrtkframework.Results;

namespace llyrtkframework.Notifications;

/// <summary>
/// 通知サービスのインターフェース
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 通知を送信します
    /// </summary>
    Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知を送信します（簡易版）
    /// </summary>
    Task<Result> SendAsync(string title, string message, NotificationType type = NotificationType.Information);

    /// <summary>
    /// 通知イベントを購読します
    /// </summary>
    IObservable<Notification> Notifications { get; }

    /// <summary>
    /// 特定のタイプの通知を購読します
    /// </summary>
    IObservable<Notification> GetNotificationsByType(NotificationType type);
}

/// <summary>
/// 通知の種類
/// </summary>
public enum NotificationType
{
    Information,
    Success,
    Warning,
    Error
}

/// <summary>
/// 通知
/// </summary>
public class Notification
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required NotificationType Type { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public TimeSpan? Duration { get; init; }
    public bool IsRead { get; set; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

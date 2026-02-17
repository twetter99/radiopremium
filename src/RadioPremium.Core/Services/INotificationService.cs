namespace RadioPremium.Core.Services;

/// <summary>
/// Service for displaying Windows notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Show a simple text notification
    /// </summary>
    void ShowNotification(string title, string message, NotificationPriority priority = NotificationPriority.Normal);

    /// <summary>
    /// Show a notification with an image
    /// </summary>
    void ShowNotificationWithImage(string title, string message, string imageUrl, NotificationPriority priority = NotificationPriority.Normal);

    /// <summary>
    /// Show a track identified notification with artwork and actions
    /// </summary>
    void ShowTrackIdentifiedNotification(string trackTitle, string artist, string? artworkUrl, string? stationName);

    /// <summary>
    /// Clear all notifications
    /// </summary>
    void ClearAllNotifications();

    /// <summary>
    /// Check if notifications are supported
    /// </summary>
    bool IsSupported { get; }
}

public enum NotificationPriority
{
    Normal,
    High
}

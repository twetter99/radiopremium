using RadioPremium.Core.Services;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Windows Toast Notifications implementation
/// </summary>
public sealed class WindowsNotificationService : INotificationService
{
    private const string AppId = "RadioPremium";
    private readonly ToastNotifier? _notifier;
    private readonly bool _isSupported;

    public bool IsSupported => _isSupported;

    public WindowsNotificationService()
    {
        try
        {
            _notifier = ToastNotificationManager.CreateToastNotifier(AppId);
            _isSupported = true;
        }
        catch
        {
            _isSupported = false;
        }
    }

    public void ShowNotification(string title, string message, NotificationPriority priority = NotificationPriority.Normal)
    {
        if (!_isSupported || _notifier == null) return;

        try
        {
            var toastXml = CreateBasicToastXml(title, message);
            var toast = new ToastNotification(toastXml);

            if (priority == NotificationPriority.High)
            {
                toast.Priority = ToastNotificationPriority.High;
            }

            _notifier.Show(toast);
        }
        catch
        {
            // Fail silently
        }
    }

    public void ShowNotificationWithImage(string title, string message, string imageUrl, NotificationPriority priority = NotificationPriority.Normal)
    {
        if (!_isSupported || _notifier == null) return;

        try
        {
            var toastXml = CreateImageToastXml(title, message, imageUrl);
            var toast = new ToastNotification(toastXml);

            if (priority == NotificationPriority.High)
            {
                toast.Priority = ToastNotificationPriority.High;
            }

            _notifier.Show(toast);
        }
        catch
        {
            // Fail silently
        }
    }

    public void ShowTrackIdentifiedNotification(string trackTitle, string artist, string? artworkUrl, string? stationName)
    {
        if (!_isSupported || _notifier == null) return;

        try
        {
            var toastXml = CreateTrackIdentifiedToastXml(trackTitle, artist, artworkUrl, stationName);
            var toast = new ToastNotification(toastXml);
            toast.Priority = ToastNotificationPriority.High;

            // Auto-dismiss after 10 seconds
            toast.ExpirationTime = DateTimeOffset.Now.AddSeconds(10);

            _notifier.Show(toast);
        }
        catch
        {
            // Fail silently
        }
    }

    public void ClearAllNotifications()
    {
        if (!_isSupported) return;

        try
        {
            ToastNotificationManager.History.Clear(AppId);
        }
        catch
        {
            // Fail silently
        }
    }

    private static XmlDocument CreateBasicToastXml(string title, string message)
    {
        var toastXml = new XmlDocument();
        toastXml.LoadXml($@"
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{EscapeXml(title)}</text>
            <text>{EscapeXml(message)}</text>
        </binding>
    </visual>
    <audio src='ms-winsoundevent:Notification.Default' />
</toast>");

        return toastXml;
    }

    private static XmlDocument CreateImageToastXml(string title, string message, string imageUrl)
    {
        var toastXml = new XmlDocument();
        toastXml.LoadXml($@"
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{EscapeXml(title)}</text>
            <text>{EscapeXml(message)}</text>
            <image placement='appLogoOverride' hint-crop='circle' src='{EscapeXml(imageUrl)}'/>
        </binding>
    </visual>
    <audio src='ms-winsoundevent:Notification.Default' />
</toast>");

        return toastXml;
    }

    private static XmlDocument CreateTrackIdentifiedToastXml(string trackTitle, string artist, string? artworkUrl, string? stationName)
    {
        var imageHtml = !string.IsNullOrEmpty(artworkUrl)
            ? $"<image placement='appLogoOverride' hint-crop='circle' src='{EscapeXml(artworkUrl)}'/>"
            : "";

        var stationText = !string.IsNullOrEmpty(stationName)
            ? $"<text placement='attribution'>En {EscapeXml(stationName)}</text>"
            : "";

        var toastXml = new XmlDocument();
        toastXml.LoadXml($@"
<toast scenario='incomingCall' launch='action=viewTrack'>
    <visual>
        <binding template='ToastGeneric'>
            <text>🎵 Canción identificada</text>
            <text>{EscapeXml(trackTitle)}</text>
            <text>{EscapeXml(artist)}</text>
            {stationText}
            {imageHtml}
        </binding>
    </visual>
    <actions>
        <action
            content='Buscar en Spotify'
            arguments='action=spotify'
            activationType='foreground'/>
        <action
            content='Ver historial'
            arguments='action=history'
            activationType='foreground'/>
    </actions>
    <audio src='ms-winsoundevent:Notification.Looping.Call' loop='false' />
</toast>");

        return toastXml;
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}

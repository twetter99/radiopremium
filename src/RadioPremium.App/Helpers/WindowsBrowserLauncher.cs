using System.Diagnostics;

namespace RadioPremium.App.Helpers;

/// <summary>
/// Launches URLs in a Windows browser directly, bypassing Parallels' URL interception
/// that would otherwise redirect to macOS browsers.
/// </summary>
public static class WindowsBrowserLauncher
{
    /// <summary>
    /// Opens a URL in a Windows browser (Edge, Chrome, or Firefox).
    /// Uses explicit browser executable path to prevent Parallels from
    /// intercepting the URL and opening it in macOS.
    /// </summary>
    public static void OpenUrl(string url)
    {
        // Try browsers in order of preference (Edge is always on Windows 10/11)
        var browsers = new[]
        {
            // Microsoft Edge (always installed on Windows 10/11)
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            // Google Chrome
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            // Firefox
            @"C:\Program Files\Mozilla Firefox\firefox.exe",
            @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe",
        };

        foreach (var browser in browsers)
        {
            if (File.Exists(browser))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = browser,
                        Arguments = url,
                        UseShellExecute = false
                    });
                    return;
                }
                catch
                {
                    continue;
                }
            }
        }

        // Last resort: use cmd.exe start command (less likely to be intercepted by Parallels)
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" \"{url}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // Final fallback
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}

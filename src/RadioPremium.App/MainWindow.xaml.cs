using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using WinRT.Interop;
using RadioPremium.App.Helpers;
using RadioPremium.Core.Services;
using System.IO;
using System.Runtime.InteropServices;

namespace RadioPremium.App;

/// <summary>
/// Main application window
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;

    // Win32 interop for removing window border
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    
    // Corner preference values
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_ROUNDSMALL = 3;

    // Prevent Windows from auto-restarting the app after system restart
    [DllImport("kernel32.dll")]
    private static extern int UnregisterApplicationRestart();

    public MainWindow()
    {
        InitializeComponent();

        // Prevent Windows from auto-restarting this app after system restart/shutdown
        UnregisterApplicationRestart();

        // Get AppWindow for customization
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Configure window
        _appWindow.Title = "Radio Premium";
        
        // Set icon using absolute path
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "radio.ico");
        if (File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
        }

        // === CRITICAL: Set title bar caption color using DWM to match background ===
        // Color #121212 in COLORREF format (BGR): 0x00121212
        int captionColor = 0x00121212; // BGR format for #121212
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        
        // Remove window border using DWM
        int borderColor = 0x00121212; // Same as caption for seamless look
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
        
        // Set window corner preference to rounded (Windows 11 style)
        int cornerPreference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

        // Extend content into title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Customize title bar button colors
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            
            // Button colors for dark theme
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(60, 255, 255, 255);
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(128, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonPressedForegroundColor = Colors.White;
        }

        // Set minimum size and presenter options
        var presenter = _appWindow.Presenter as OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            // Start maximized for better navigation experience
            presenter.Maximize();
        }

        // Set initial size as fallback (if not maximized)
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));

        // Center on screen
        CenterOnScreen();

        // Setup navigation service
        var navigationService = App.GetService<INavigationService>() as NavigationService;
        navigationService?.Initialize(ShellFrame.NavigationFrame);

        // Register keyboard accelerators
        RegisterKeyboardShortcuts();
    }

    private void CenterOnScreen()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var centerX = (displayArea.WorkArea.Width - _appWindow.Size.Width) / 2;
        var centerY = (displayArea.WorkArea.Height - _appWindow.Size.Height) / 2;
        _appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
    }

    private void RegisterKeyboardShortcuts()
    {
        // Space - Play/Pause
        var playPauseAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Space,
            Modifiers = Windows.System.VirtualKeyModifiers.None
        };
        playPauseAccelerator.Invoked += (s, e) =>
        {
            ShellFrame.TogglePlayPause();
            e.Handled = true;
        };

        // Ctrl+I - Identify
        var identifyAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.I,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        identifyAccelerator.Invoked += (s, e) =>
        {
            ShellFrame.StartIdentification();
            e.Handled = true;
        };

        // Ctrl+Shift+S - Add to Spotify
        var spotifyAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.S,
            Modifiers = Windows.System.VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Shift
        };
        spotifyAccelerator.Invoked += (s, e) =>
        {
            ShellFrame.AddToSpotify();
            e.Handled = true;
        };

        // Ctrl+, - Settings
        var settingsAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = (Windows.System.VirtualKey)188, // Comma
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        settingsAccelerator.Invoked += (s, e) =>
        {
            ShellFrame.NavigateToSettings();
            e.Handled = true;
        };

        Content.KeyboardAccelerators.Add(playPauseAccelerator);
        Content.KeyboardAccelerators.Add(identifyAccelerator);
        Content.KeyboardAccelerators.Add(spotifyAccelerator);
        Content.KeyboardAccelerators.Add(settingsAccelerator);
    }
}

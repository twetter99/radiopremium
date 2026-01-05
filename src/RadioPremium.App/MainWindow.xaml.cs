using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using WinRT.Interop;
using RadioPremium.App.Helpers;
using RadioPremium.Core.Services;

namespace RadioPremium.App;

/// <summary>
/// Main application window
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        // Get AppWindow for customization
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Configure window
        _appWindow.Title = "Radio Premium";
        _appWindow.SetIcon("Assets/radio.ico");

        // Set minimum size and presenter options
        var presenter = _appWindow.Presenter as OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        // Set initial size (larger for better UX)
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1600, 1000));

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

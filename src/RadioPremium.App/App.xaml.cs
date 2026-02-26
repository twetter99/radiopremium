using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using RadioPremium.App.Helpers;
using RadioPremium.App.Views;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using RadioPremium.Core.ViewModels;
using RadioPremium.Infrastructure.Services;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RadioPremium.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private MiniPlayerWindow? _miniPlayerWindow;

    /// <summary>
    /// Tracks the time when the system was suspended.
    /// Used to detect prolonged suspends (e.g. Parallels VM shutdown)
    /// and close the app on resume instead of persisting across sessions.
    /// </summary>
    private DateTime _suspendedAt = DateTime.MinValue;

    /// <summary>
    /// Minimum suspend duration to trigger automatic app exit on resume.
    /// If the system was suspended for longer than this, the app closes itself
    /// because it likely means the host machine was shut down (Parallels VM).
    /// </summary>
    private static readonly TimeSpan SuspendExitThreshold = TimeSpan.FromMinutes(2);

    // Prevent Windows from auto-restarting the app after system restart
    [DllImport("kernel32.dll")]
    private static extern int UnregisterApplicationRestart();

    public static IServiceProvider Services { get; private set; } = null!;

    public static Window? MainWindow => ((App)Current)._window;

    public static void SwitchToMiniPlayer()
    {
        var app = (App)Current;
        if (app._miniPlayerWindow is null)
        {
            app._miniPlayerWindow = new MiniPlayerWindow();
            app._miniPlayerWindow.ExpandRequested += (s, e) => SwitchToMainWindow();
            app._miniPlayerWindow.Closed += (s, e) =>
            {
                app._miniPlayerWindow = null;
                // If main window is also closed, exit app
                if (app._window is null)
                {
                    app.Exit();
                }
            };
        }

        app._miniPlayerWindow.Activate();

        // Close main window when switching to mini player
        var mainWindow = app._window;
        app._window = null;
        mainWindow?.Close();
    }

    public static void SwitchToMainWindow()
    {
        var app = (App)Current;

        // Create new main window
        app._window = new MainWindow();
        app._window.Activate();

        // Close mini player
        var miniPlayer = app._miniPlayerWindow;
        app._miniPlayerWindow = null;
        miniPlayer?.Close();
    }

    public static T GetService<T>() where T : class => Services.GetRequiredService<T>();

    public App()
    {
        UnhandledException += App_UnhandledException;
        InitializeComponent();

        // Prevent Windows from auto-restarting this app after system restart/shutdown.
        // Called early, before any window is created, so Windows never registers us.
        UnregisterApplicationRestart();

        try
        {
            Services = ConfigureServices();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error configuring services: {ex}");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "error.log"), ex.ToString());
            throw;
        }

        // Listen for system power events (suspend/hibernate/resume) and session ending.
        // When running inside a VM (e.g. Parallels), closing the VM suspends Windows.
        // On suspend: record timestamp and stop audio.
        // On resume: if suspended too long (VM was shut down), exit the app entirely.
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception.ToString());
        e.Handled = true;
    }

    /// <summary>
    /// Handles system power mode changes (suspend, resume, battery status).
    /// <para>
    /// <b>Suspend:</b> Record the current time and stop playback immediately.
    /// This runs just before the OS freezes all processes (Parallels VM close/pause).
    /// </para>
    /// <para>
    /// <b>Resume:</b> If the suspend lasted longer than <see cref="SuspendExitThreshold"/>,
    /// it means the host was shut down (e.g. Mac turned off with Parallels).
    /// In that case we exit the app entirely so it doesn't linger from a previous session.
    /// For brief suspends (laptop lid close), we just stop playback as a safety measure.
    /// </para>
    /// </summary>
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        try
        {
            if (e.Mode == PowerModes.Suspend)
            {
                _suspendedAt = DateTime.UtcNow;
                var audioPlayer = Services.GetService<IAudioPlayerService>();
                audioPlayer?.Stop();
                System.Diagnostics.Debug.WriteLine($"[RadioPremium] System suspending. Timestamp recorded, playback stopped.");
            }
            else if (e.Mode == PowerModes.Resume)
            {
                var elapsed = DateTime.UtcNow - _suspendedAt;
                System.Diagnostics.Debug.WriteLine($"[RadioPremium] System resumed after {elapsed.TotalMinutes:F1} min.");

                // Always stop playback on resume
                var audioPlayer = Services.GetService<IAudioPlayerService>();
                audioPlayer?.Stop();

                if (_suspendedAt != DateTime.MinValue && elapsed > SuspendExitThreshold)
                {
                    // Prolonged suspend detected (Parallels/VM shutdown).
                    // Exit the app so it doesn't persist across sessions.
                    System.Diagnostics.Debug.WriteLine($"[RadioPremium] Prolonged suspend detected ({elapsed.TotalMinutes:F1} min > {SuspendExitThreshold.TotalMinutes} min). Exiting app.");
                    CleanupAndExit();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RadioPremium] Error handling power mode {e.Mode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles session ending (logoff, shutdown, restart).
    /// Ensures audio is stopped and the application exits completely.
    /// </summary>
    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[RadioPremium] Session ending: {e.Reason}. Cleaning up and exiting.");
        CleanupAndExit();
    }

    /// <summary>
    /// Stops all services, disposes resources, and exits the application.
    /// Safe to call from any thread — dispatches to UI thread if needed.
    /// </summary>
    private void CleanupAndExit()
    {
        try
        {
            // Stop audio and dispose services
            var audioPlayer = Services.GetService<IAudioPlayerService>();
            audioPlayer?.Stop();
            (audioPlayer as IDisposable)?.Dispose();

            var loopback = Services.GetService<ILoopbackCaptureService>();
            (loopback as IDisposable)?.Dispose();

            // Unsubscribe from system events to prevent callbacks after exit
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;

            System.Diagnostics.Debug.WriteLine($"[RadioPremium] Cleanup complete. Calling Exit().");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RadioPremium] Error during cleanup: {ex.Message}");
        }

        // Marshal Exit() call to the UI thread (SystemEvents fire on a worker thread)
        try
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue != null)
            {
                Exit();
            }
            else
            {
                // We're on a worker thread — post to the main window's dispatcher
                var window = _window ?? (_miniPlayerWindow as Window);
                if (window?.DispatcherQueue != null)
                {
                    window.DispatcherQueue.TryEnqueue(() => Exit());
                }
                else
                {
                    // Last resort: terminate the process directly
                    Environment.Exit(0);
                }
            }
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Load settings from appsettings.json
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var settingsJson = File.ReadAllText(settingsPath);
        var config = JsonSerializer.Deserialize<AppConfiguration>(settingsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new AppConfiguration();

        // Register settings
        services.AddSingleton(config.AcrCloud);
        services.AddSingleton(config.Spotify);

        // Register HttpClient
        services.AddHttpClient();

        // Register Infrastructure Services
        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<IFavoritesRepository, FavoritesRepository>();
        services.AddSingleton<IIdentifiedTracksRepository, IdentifiedTracksRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        services.AddSingleton<ILoopbackCaptureService, LoopbackCaptureService>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<IPlaybackQueueService, PlaybackQueueService>();

        services.AddSingleton<IRadioBrowserService>(sp =>
        {
            var client = new HttpClient();
            return new RadioBrowserService(client);
        });

        services.AddSingleton<IAcrCloudRecognitionService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var settings = sp.GetRequiredService<AcrCloudSettings>();
            return new AcrCloudRecognitionService(factory.CreateClient("AcrCloud"), settings);
        });

        services.AddSingleton<ISpotifyAuthService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var storage = sp.GetRequiredService<ISecureStorageService>();
            var settings = sp.GetRequiredService<SpotifySettings>();
            return new SpotifyAuthService(factory.CreateClient("SpotifyAuth"), storage, settings);
        });

        services.AddSingleton<ISpotifyApiService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var auth = sp.GetRequiredService<ISpotifyAuthService>();
            return new SpotifyApiService(factory.CreateClient("SpotifyApi"), auth);
        });

        // Register Navigation Service
        services.AddSingleton<INavigationService, NavigationService>();

        // Register ViewModels
        services.AddTransient<ShellViewModel>();
        services.AddTransient<RadioViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<IdentifyViewModel>();
        services.AddTransient<SpotifyViewModel>();
        services.AddTransient<FavoritesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<IdentifiedTracksViewModel>();

        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Load user settings from disk before creating any window/ViewModel
        var settingsService = Services.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Initialize PlayerViewModel volume from persisted settings
        var playerVm = Services.GetRequiredService<PlayerViewModel>();
        playerVm.ApplySettings(settingsService.Settings);

        _window = new MainWindow();
        _window.Activate();
    }

    private sealed class AppConfiguration
    {
        public AcrCloudSettings AcrCloud { get; set; } = new();
        public SpotifySettings Spotify { get; set; } = new();
    }
}

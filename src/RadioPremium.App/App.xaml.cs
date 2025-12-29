using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using RadioPremium.App.Helpers;
using RadioPremium.App.Views;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using RadioPremium.Core.ViewModels;
using RadioPremium.Infrastructure.Services;
using System.Text.Json;

namespace RadioPremium.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public static IServiceProvider Services { get; private set; } = null!;

    public static T GetService<T>() where T : class => Services.GetRequiredService<T>();

    public App()
    {
        UnhandledException += App_UnhandledException;
        InitializeComponent();
        
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
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception.ToString());
        e.Handled = true;
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
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        services.AddSingleton<ILoopbackCaptureService, LoopbackCaptureService>();

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

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private sealed class AppConfiguration
    {
        public AcrCloudSettings AcrCloud { get; set; } = new();
        public SpotifySettings Spotify { get; set; } = new();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for settings page
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ISpotifyAuthService _spotifyAuthService;

    [ObservableProperty]
    private float _volume;

    [ObservableProperty]
    private bool _autoPlay;

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private int _captureSeconds = 10;

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private string _defaultCountry = "ES";

    [ObservableProperty]
    private bool _musicOnlyFilter = true;

    [ObservableProperty]
    private bool _isSpotifyConnected;

    [ObservableProperty]
    private string? _spotifyUserName;

    public string[] AvailableThemes { get; } = { "System", "Light", "Dark" };
    public int[] CaptureSecondsOptions { get; } = { 5, 10, 15, 20 };

    public SettingsViewModel(
        ISettingsService settingsService,
        ISpotifyAuthService spotifyAuthService)
    {
        _settingsService = settingsService;
        _spotifyAuthService = spotifyAuthService;

        _spotifyAuthService.AuthenticationStateChanged += (s, e) =>
        {
            IsSpotifyConnected = e;
        };
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        LoadFromSettings();
        IsSpotifyConnected = _spotifyAuthService.IsAuthenticated;
    }

    private void LoadFromSettings()
    {
        var settings = _settingsService.Settings;
        Volume = settings.Volume;
        AutoPlay = settings.AutoPlay;
        Theme = settings.Theme;
        CaptureSeconds = settings.CaptureSeconds;
        ShowNotifications = settings.ShowNotifications;
        DefaultCountry = settings.DefaultCountry;
        MusicOnlyFilter = settings.MusicOnlyFilter;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = _settingsService.Settings;
        settings.Volume = Volume;
        settings.AutoPlay = AutoPlay;
        settings.Theme = Theme;
        settings.CaptureSeconds = CaptureSeconds;
        settings.ShowNotifications = ShowNotifications;
        settings.DefaultCountry = DefaultCountry;
        settings.MusicOnlyFilter = MusicOnlyFilter;

        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        await _settingsService.ResetAsync();
        LoadFromSettings();
    }

    [RelayCommand]
    private async Task DisconnectSpotifyAsync()
    {
        await _spotifyAuthService.LogoutAsync();
        IsSpotifyConnected = false;
        SpotifyUserName = null;
    }

    partial void OnVolumeChanged(float value) => _ = SaveSettingsAsync();
    partial void OnAutoPlayChanged(bool value) => _ = SaveSettingsAsync();
    partial void OnThemeChanged(string value) => _ = SaveSettingsAsync();
    partial void OnCaptureSecondsChanged(int value) => _ = SaveSettingsAsync();
    partial void OnShowNotificationsChanged(bool value) => _ = SaveSettingsAsync();
    partial void OnDefaultCountryChanged(string value) => _ = SaveSettingsAsync();
    partial void OnMusicOnlyFilterChanged(bool value) => _ = SaveSettingsAsync();
}

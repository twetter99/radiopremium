namespace RadioPremium.Core.Models;

/// <summary>
/// Application settings
/// </summary>
public sealed class AppSettings
{
    public float Volume { get; set; } = 0.8f;
    public bool AutoPlay { get; set; }
    public string? LastStationId { get; set; }
    public string Theme { get; set; } = "System";
    public int CaptureSeconds { get; set; } = 10;
    public bool ShowNotifications { get; set; } = true;
    public string DefaultCountry { get; set; } = "ES";
    public int SearchResultLimit { get; set; } = 50;
    public bool MusicOnlyFilter { get; set; } = true; // Only show music stations
    public bool StartInMiniPlayer { get; set; } = false; // Start app in mini-player mode
}

/// <summary>
/// Configuration for ACRCloud service
/// </summary>
public sealed class AcrCloudSettings
{
    public string Host { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string AccessSecret { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for Spotify service
/// </summary>
public sealed class SpotifySettings
{
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;

    public const string PlaylistName = "Radio Likes";
    public const string PlaylistDescription = "Canciones identificadas desde Radio Premium";
}

/// <summary>
/// Navigation page types
/// </summary>
public enum PageType
{
    Radio,
    Favorites,
    History,
    Settings
}

namespace RadioPremium.Core.Models;

/// <summary>
/// Represents a recognized track from ACRCloud
/// </summary>
public sealed class Track
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string? ArtworkUrl { get; set; }
    public string? Isrc { get; set; }
    public string? SpotifyId { get; set; }
    public string? AppleMusicId { get; set; }
    public string? DeezerTrackId { get; set; }
    public string? YoutubeVideoId { get; set; }
    public int? DurationMs { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Label { get; set; }
    public string? Genre { get; set; }
    public int Score { get; set; }
    public DateTime RecognizedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Returns the search query for Spotify
    /// </summary>
    public string SpotifySearchQuery => $"track:{Title} artist:{Artist}";

    /// <summary>
    /// Returns formatted display string
    /// </summary>
    public string DisplayText => $"{Artist} - {Title}";

    /// <summary>
    /// Returns formatted duration string
    /// </summary>
    public string DurationFormatted => DurationMs.HasValue 
        ? TimeSpan.FromMilliseconds(DurationMs.Value).ToString(@"m\:ss") 
        : "--:--";

    public override string ToString() => DisplayText;
}

/// <summary>
/// Result of track identification operation
/// </summary>
public sealed class IdentificationResult
{
    public bool Success { get; set; }
    public Track? Track { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public string? StatusMessage { get; set; }

    public static IdentificationResult FromSuccess(Track track) => new()
    {
        Success = true,
        Track = track,
        StatusCode = 0,
        StatusMessage = "Success"
    };

    public static IdentificationResult FromError(int code, string message) => new()
    {
        Success = false,
        StatusCode = code,
        StatusMessage = message,
        ErrorMessage = message
    };

    public static IdentificationResult NoMatch() => new()
    {
        Success = false,
        StatusCode = 1001,
        StatusMessage = "No result",
        ErrorMessage = "No se pudo identificar la canción"
    };
}

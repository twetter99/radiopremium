namespace RadioPremium.Core.Models;

/// <summary>
/// Spotify user profile
/// </summary>
public sealed class SpotifyUser
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;

    public bool IsPremium => Product == "premium";
}

/// <summary>
/// Spotify playlist
/// </summary>
public sealed class SpotifyPlaylist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Uri { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool IsCollaborative { get; set; }
    public SpotifyUser? Owner { get; set; }
    public SpotifyTracks? Tracks { get; set; }
}

public sealed class SpotifyTracks
{
    public int Total { get; set; }
    public string? Href { get; set; }
}

/// <summary>
/// Spotify track
/// </summary>
public sealed class SpotifyTrack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public int TrackNumber { get; set; }
    public bool IsPlayable { get; set; }
    public SpotifyAlbum? Album { get; set; }
    public List<SpotifyArtist> Artists { get; set; } = new();
    public SpotifyExternalIds? ExternalIds { get; set; }

    public string PrimaryArtist => Artists.FirstOrDefault()?.Name ?? "Unknown";
    public string AlbumName => Album?.Name ?? string.Empty;
    public string? ArtworkUrl => Album?.Images?.FirstOrDefault()?.Url;
}

public sealed class SpotifyAlbum
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string? ReleaseDate { get; set; }
    public List<SpotifyImage>? Images { get; set; }
}

public sealed class SpotifyArtist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
}

public sealed class SpotifyImage
{
    public string Url { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public sealed class SpotifyExternalIds
{
    public string? Isrc { get; set; }
}

/// <summary>
/// OAuth tokens for Spotify
/// </summary>
public sealed class SpotifyTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Scope { get; set; } = string.Empty;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}

/// <summary>
/// Spotify search results
/// </summary>
public sealed class SpotifySearchResult
{
    public SpotifyTrackSearchResult? Tracks { get; set; }
}

public sealed class SpotifyTrackSearchResult
{
    public List<SpotifyTrack> Items { get; set; } = new();
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

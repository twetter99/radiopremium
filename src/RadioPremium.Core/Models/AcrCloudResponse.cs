using System.Text.Json.Serialization;

namespace RadioPremium.Core.Models;

/// <summary>
/// Response from ACRCloud API
/// </summary>
public sealed class AcrCloudResponse
{
    [JsonPropertyName("status")]
    public AcrCloudStatus Status { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public AcrCloudMetadata? Metadata { get; set; }
    
    [JsonPropertyName("cost_time")]
    public double CostTime { get; set; }
    
    [JsonPropertyName("result_type")]
    public int ResultType { get; set; }
}

public sealed class AcrCloudStatus
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;
    
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public sealed class AcrCloudMetadata
{
    [JsonPropertyName("music")]
    public List<AcrCloudMusic> Music { get; set; } = new();
    
    [JsonPropertyName("timestamp_utc")]
    public string TimestampUtc { get; set; } = string.Empty;
}

public sealed class AcrCloudMusic
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("artists")]
    public List<AcrCloudArtist> Artists { get; set; } = new();
    
    [JsonPropertyName("album")]
    public AcrCloudAlbum? Album { get; set; }
    
    [JsonPropertyName("isrc")]
    public string Isrc { get; set; } = string.Empty;
    
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
    
    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = string.Empty;
    
    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }
    
    [JsonPropertyName("score")]
    public int Score { get; set; }
    
    [JsonPropertyName("play_offset_ms")]
    public int PlayOffsetMs { get; set; }
    
    [JsonPropertyName("external_ids")]
    public AcrCloudExternalIds? ExternalIds { get; set; }
    
    [JsonPropertyName("external_metadata")]
    public AcrCloudExternalMetadata? ExternalMetadata { get; set; }
    
    [JsonPropertyName("genres")]
    public List<AcrCloudGenre>? Genres { get; set; }

    public string PrimaryArtist => Artists.FirstOrDefault()?.Name ?? "Unknown Artist";
    public string AlbumName => Album?.Name ?? string.Empty;
}

public sealed class AcrCloudGenre
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AcrCloudArtist
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AcrCloudAlbum
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AcrCloudExternalIds
{
    [JsonPropertyName("isrc")]
    public string? Isrc { get; set; }
    
    [JsonPropertyName("upc")]
    public string? Upc { get; set; }
}

public sealed class AcrCloudExternalMetadata
{
    [JsonPropertyName("spotify")]
    public AcrCloudSpotifyData? Spotify { get; set; }
    
    [JsonPropertyName("deezer")]
    public AcrCloudDeezerData? Deezer { get; set; }
    
    [JsonPropertyName("youtube")]
    public AcrCloudYoutube? Youtube { get; set; }
}

public sealed class AcrCloudSpotifyData
{
    [JsonPropertyName("album")]
    public AcrCloudSpotifyAlbum? Album { get; set; }
    
    [JsonPropertyName("artists")]
    public List<AcrCloudSpotifyArtist>? Artists { get; set; }
    
    [JsonPropertyName("track")]
    public AcrCloudSpotifyTrackInfo? Track { get; set; }
}

public sealed class AcrCloudSpotifyTrackInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AcrCloudSpotifyAlbum
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AcrCloudSpotifyArtist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AcrCloudSpotifyUrls
{
    [JsonPropertyName("spotify")]
    public string? Spotify { get; set; }
}

public sealed class AcrCloudDeezerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("album")]
    public AcrCloudDeezerAlbum? Album { get; set; }
    
    [JsonPropertyName("artists")]
    public List<AcrCloudDeezerArtist>? Artists { get; set; }
}

public sealed class AcrCloudDeezerAlbum
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("cover_big")]
    public string CoverBig { get; set; } = string.Empty;
}

public sealed class AcrCloudDeezerArtist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AcrCloudYoutube
{
    [JsonPropertyName("vid")]
    public string? Vid { get; set; }
}
